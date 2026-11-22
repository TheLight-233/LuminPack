#pragma once

#include <algorithm>
#include <memory>
#include <unordered_map>
#include <utility>

// Included by luminpack.h after the binary cursors and checked errors.
namespace luminpack
{
/// Owns tracked objects independently of their links, which borrow this graph's lifetime.
class reference_graph
{
public:
    reference_graph() = default;
    reference_graph(reference_graph&&) noexcept = default;
    reference_graph& operator=(reference_graph&&) noexcept = default;
    reference_graph(const reference_graph&) = delete;
    reference_graph& operator=(const reference_graph&) = delete;

    /// Creates an object whose address stays stable until the graph is destroyed.
    template<class T, class... Args>
    [[nodiscard]] T* make(Args&&... args)
    {
        return own(std::make_unique<T>(std::forward<Args>(args)...));
    }

    /// Transfers an already created object to the graph.
    template<class T>
    [[nodiscard]] T* own(std::unique_ptr<T> value)
    {
        auto* result = value.get();
        if (result)
        {
            node erased(value.release(), [](void* pointer) { delete static_cast<T*>(pointer); });
            nodes_.push_back(std::move(erased));
        }
        return result;
    }

private:
    using node = std::unique_ptr<void, void (*)(void*)>;
    std::vector<node> nodes_;
    friend class reference_read_state;
};

/// A decoded document and the owner of all tracked objects it references.
template<class Root>
struct reference_document
{
    reference_graph graph;
    Root value{};
};

inline constexpr std::size_t default_binary_reference_depth = 512;

namespace detail
{
// A writable inline token has a distinct address for each concrete type, without requiring RTTI.
template<class T>
inline std::byte reference_type_token{};
}

/// The checked ID table and graph ownership for a single tracked-reference read operation.
class reference_read_state
{
public:
    explicit reference_read_state(reference_graph& graph, std::size_t max_depth = default_binary_reference_depth) noexcept
        : graph_(graph), max_depth_(max_depth) {}
    reference_read_state(const reference_read_state&) = delete;
    reference_read_state& operator=(const reference_read_state&) = delete;

    class scope
    {
    public:
        explicit scope(reference_read_state& state) noexcept
            : state_(state), sequential_(state.sequential_.size()), sparse_(state.sparse_order_.size()),
              nodes_(state.graph_.nodes_.size()) {}
        ~scope() { if (!keep_) state_.truncate(sequential_, sparse_, nodes_); }
        scope(const scope&) = delete;
        scope& operator=(const scope&) = delete;

        /// Keeps successfully decoded IDs and their owned shells.
        void commit() noexcept { keep_ = true; }

    private:
        reference_read_state& state_;
        std::size_t sequential_;
        std::size_t sparse_;
        std::size_t nodes_;
        bool keep_ = false;
    };

    /// Bounds recursively nested first occurrences independently of back references.
    class depth_scope
    {
    public:
        explicit depth_scope(reference_read_state& state) : state_(state)
        {
            if (state_.max_depth_ != 0 && state_.depth_ >= state_.max_depth_)
                throw wire_error("LuminPack tracked object nesting exceeds its maximum depth.");
            ++state_.depth_;
        }
        ~depth_scope() { --state_.depth_; }
        depth_scope(const depth_scope&) = delete;
        depth_scope& operator=(const depth_scope&) = delete;

    private:
        reference_read_state& state_;
    };

    /// Returns the already registered concrete object for a back reference.
    template<class T>
    [[nodiscard]] T* get(std::uint32_t id) const
    {
        const auto& entry = id < sequential_.size() ? sequential_[id] : get_sparse(id);
        if (entry.type != &detail::reference_type_token<T>)
            throw wire_error("LuminPack back reference has the wrong object type.");
        return static_cast<T*>(entry.object);
    }

    /// Retains a pre-created shell independently of whether the stream supplies its ID.
    template<class T>
    [[nodiscard]] T* own(std::unique_ptr<T> value) { return graph_.own(std::move(value)); }

    /// Registers a shell before decoding members that can refer to it.
    template<class T>
    void add(std::uint32_t id, T* value)
    {
        if (!value)
            throw std::invalid_argument("Cannot register a null LuminPack reference shell.");
        const slot entry{value, &detail::reference_type_token<T>};
        if (!reuse_index_)
        {
            store(id, entry);
            return;
        }

        const auto [identity, inserted] = reuse_index_->try_emplace(value, 0);
        try
        {
            store(id, entry);
        }
        catch (...)
        {
            if (inserted)
                reuse_index_->erase(identity);
            throw;
        }
        ++identity->second;
    }

    /// Allocates and registers a default shell under the new object's explicit ID.
    template<class T>
    [[nodiscard]] T* add(std::uint32_t id)
    {
        scope transaction(*this);
        auto* value = graph_.make<T>();
        add(id, value);
        transaction.commit();
        return value;
    }

    /// Tests whether this operation has claimed an object, without comparing its fields.
    [[nodiscard]] bool registered(const void* value)
    {
        if (!value)
            return false;
        if (!reuse_index_)
        {
            if (sequential_.size() <= 8 && sparse_order_.empty())
                return std::any_of(sequential_.begin(), sequential_.end(), [value](const slot& entry) { return entry.object == value; });
            build_reuse_index();
        }
        return reuse_index_->contains(value);
    }

    /// Reuses a caller-supplied shell only when the current operation has not already claimed it.
    template<class T>
    [[nodiscard]] bool claim(std::uint32_t id, T* value)
    {
        if (!value)
            return false;
        if (!reuse_index_ && sequential_.size() <= 8 && sparse_order_.empty())
        {
            if (registered(value))
                return false;
            store(id, {value, &detail::reference_type_token<T>});
            return true;
        }
        if (!reuse_index_)
            build_reuse_index();

        const auto [identity, inserted] = reuse_index_->try_emplace(value, 1);
        if (!inserted)
            return false;
        try
        {
            store(id, {value, &detail::reference_type_token<T>});
        }
        catch (...)
        {
            reuse_index_->erase(identity);
            throw;
        }
        return true;
    }

private:
    struct slot
    {
        void* object;
        const void* type;
    };

    [[nodiscard]] const slot& get_sparse(std::uint32_t id) const
    {
        if (sparse_)
        {
            const auto entry = sparse_->find(id);
            if (entry != sparse_->end())
                return entry->second;
        }
        throw wire_error("Unknown LuminPack back reference ID.");
    }

    void store(std::uint32_t id, slot entry)
    {
        if (id == sequential_.size() && sparse_order_.empty())
        {
            if (sequential_.capacity() == 0)
                sequential_.reserve(16);
            sequential_.push_back(entry);
            return;
        }
        if (id < sequential_.size())
            throw wire_error("Duplicate LuminPack reference ID.");

        sparse_order_.push_back(id);
        try
        {
            if (!sparse_)
                sparse_.emplace();
            if (!sparse_->try_emplace(id, entry).second)
                throw wire_error("Duplicate LuminPack reference ID.");
        }
        catch (...)
        {
            sparse_order_.pop_back();
            throw;
        }
    }

    void build_reuse_index()
    {
        auto& index = reuse_index_.emplace();
        try
        {
            index.reserve(sequential_.size() + sparse_order_.size());
            for (const auto& entry : sequential_)
                ++index[entry.object];
            if (sparse_)
                for (const auto& entry : *sparse_)
                    ++index[entry.second.object];
        }
        catch (...)
        {
            reuse_index_.reset();
            throw;
        }
    }

    void forget(const void* value) noexcept
    {
        if (reuse_index_)
        {
            const auto entry = reuse_index_->find(value);
            if (entry != reuse_index_->end() && --entry->second == 0)
                reuse_index_->erase(entry);
        }
    }

    void truncate(std::size_t sequential, std::size_t sparse, std::size_t nodes) noexcept
    {
        while (sparse_order_.size() > sparse)
        {
            const auto entry = sparse_->find(sparse_order_.back());
            forget(entry->second.object);
            sparse_->erase(entry);
            sparse_order_.pop_back();
        }
        while (sequential_.size() > sequential)
        {
            forget(sequential_.back().object);
            sequential_.pop_back();
        }
        while (graph_.nodes_.size() > nodes)
            graph_.nodes_.pop_back();
    }

    reference_graph& graph_;
    std::size_t max_depth_;
    std::size_t depth_ = 0;
    std::vector<slot> sequential_;
    std::optional<std::unordered_map<std::uint32_t, slot>> sparse_;
    std::vector<std::uint32_t> sparse_order_;
    std::optional<std::unordered_map<const void*, std::size_t>> reuse_index_;
};

/// Assigns reference IDs in first-occurrence order for one document write.
class reference_write_state
{
public:
    class scope
    {
    public:
        explicit scope(reference_write_state& state) noexcept : state_(state), size_(state.size_) {}
        ~scope() { if (!keep_) state_.truncate(size_); }
        scope(const scope&) = delete;
        scope& operator=(const scope&) = delete;

        /// Keeps the identities accumulated by the completed write.
        void commit() noexcept { keep_ = true; }

    private:
        reference_write_state& state_;
        std::uint32_t size_;
        bool keep_ = false;
    };

    /// Returns a new ID, or the bitwise complement of the existing ID.
    [[nodiscard]] std::int32_t track(const void* value)
    {
        if (!index_)
        {
            for (std::uint32_t index = 0; index < size_; ++index)
                if (small_[index] == value)
                    return ~static_cast<std::int32_t>(index);
            if (size_ < small_.size())
            {
                small_[size_] = value;
                return static_cast<std::int32_t>(size_++);
            }
            build_index();
        }

        const auto [entry, inserted] = index_->try_emplace(value, size_);
        if (!inserted)
            return ~static_cast<std::int32_t>(entry->second);
        if (size_ > static_cast<std::uint32_t>(std::numeric_limits<std::int32_t>::max()))
        {
            index_->erase(entry);
            throw std::length_error("Too many LuminPack tracked references in one operation.");
        }
        return static_cast<std::int32_t>(size_++);
    }

private:
    void build_index()
    {
        auto& index = index_.emplace();
        try
        {
            index.reserve(small_.size() + 1);
            for (std::uint32_t position = 0; position < small_.size(); ++position)
                index.emplace(small_[position], position);
        }
        catch (...)
        {
            index_.reset();
            throw;
        }
    }

    void truncate(std::uint32_t size) noexcept
    {
        if (index_ && size != size_)
        {
            if (size <= small_.size())
                index_.reset();
            else
            {
                for (auto entry = index_->begin(); entry != index_->end();)
                {
                    if (entry->second >= size)
                        entry = index_->erase(entry);
                    else
                        ++entry;
                }
            }
        }
        size_ = size;
    }

    std::array<const void*, 8> small_{};
    std::uint32_t size_ = 0;
    std::optional<std::unordered_map<const void*, std::uint32_t>> index_;
};
}
