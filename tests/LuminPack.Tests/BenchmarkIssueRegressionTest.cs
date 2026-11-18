#if LUMINPACK_NEXT_GENERATOR
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

// Issue #2: a value-tuple member used to emit ReadValue/WriteValue twice (CS0111).
/// <summary>Carries a named value tuple beside plain fields.</summary>
[LuminPackable]
public partial struct BenchWaypoint
{
    /// <summary>Gets or sets the first coordinate.</summary>
    public float X;
    /// <summary>Gets or sets the second coordinate.</summary>
    public float Y;
    /// <summary>Gets or sets the named value tuple.</summary>
    public (int Tick, short Zone) Stamp;
    /// <summary>Gets or sets the visited flag.</summary>
    public bool Visited;
}

// Issue #3: a list of objects inside an element of a list of objects used to declare 'element' twice (CS0136).
/// <summary>Holds a page of questions with nested comments and answers.</summary>
[LuminPackable]
public sealed partial class BenchPage
{
    /// <summary>Gets or sets the page items.</summary>
    public List<BenchQuestion> Items = [];
    /// <summary>Gets or sets whether more pages follow.</summary>
    public bool HasMore;
    /// <summary>Gets or sets the page number.</summary>
    public int Number;
}

/// <summary>Holds one question and its nested lists.</summary>
[LuminPackable]
public sealed partial class BenchQuestion
{
    /// <summary>Gets or sets the identifier.</summary>
    public int QuestionId;
    /// <summary>Gets or sets the title.</summary>
    public string Title = string.Empty;
    /// <summary>Gets or sets the comments.</summary>
    public List<BenchComment> Comments = [];
    /// <summary>Gets or sets the answers.</summary>
    public List<BenchAnswer> Answers = [];
}

/// <summary>Holds one answer and its nested comments.</summary>
[LuminPackable]
public sealed partial class BenchAnswer
{
    /// <summary>Gets or sets the identifier.</summary>
    public int AnswerId;
    /// <summary>Gets or sets the comments.</summary>
    public List<BenchComment> Comments = [];
}

/// <summary>Holds one comment.</summary>
[LuminPackable]
public sealed partial class BenchComment
{
    /// <summary>Gets or sets the identifier.</summary>
    public int CommentId;
    /// <summary>Gets or sets the body.</summary>
    public string Body = string.Empty;
}

// Issue #5: a generic polymorphic base closed over tuples, with annotated subtypes.
/// <summary>Provides an abstract generic base that is closed over each tuple element shape.</summary>
/// <typeparam name="T">The stored value.</typeparam>
[LuminPackable]
public abstract partial class BenchTupleBox<T>
{
    /// <summary>Gets or sets the stored value.</summary>
    public T Value = default!;
}

/// <summary>Closes the generic base over two integers.</summary>
[LuminPackable]
public sealed partial class BenchTupleIntCase : BenchTupleBox<(int, long)>
{
}

/// <summary>Closes the generic base over an integer and a string.</summary>
[LuminPackable]
public sealed partial class BenchTupleStringCase : BenchTupleBox<(int, string)>
{
}

/// <summary>Provides the sealed tuple element.</summary>
[LuminPackable]
public sealed partial class BenchSealedItem
{
    /// <summary>Gets or sets the first field.</summary>
    public int X;
    /// <summary>Gets or sets the second field.</summary>
    public long Y;
}

/// <summary>Closes the generic base over a sealed reference element.</summary>
[LuminPackable]
public sealed partial class BenchTupleSealedCase : BenchTupleBox<(BenchSealedItem, int)>
{
}

/// <summary>Provides the open tuple element.</summary>
[LuminPackable]
public partial class BenchOpenItem
{
    /// <summary>Gets or sets the first field.</summary>
    public int X;
    /// <summary>Gets or sets the second field.</summary>
    public long Y;
}

/// <summary>Closes the generic base over an open reference element.</summary>
[LuminPackable]
public sealed partial class BenchTupleOpenCase : BenchTupleBox<(BenchOpenItem, int)>
{
}

/// <summary>Holds members typed as each closed generic base.</summary>
[LuminPackable]
public sealed partial class BenchTupleBatch
{
    /// <summary>Gets or sets the integer tuple case.</summary>
    public BenchTupleBox<(int, long)> Tuple = null!;
    /// <summary>Gets or sets the string tuple case.</summary>
    public BenchTupleBox<(int, string)> StringTuple = null!;
    /// <summary>Gets or sets the sealed element tuple case.</summary>
    public BenchTupleBox<(BenchSealedItem, int)> SealedTuple = null!;
    /// <summary>Gets or sets the open element tuple case.</summary>
    public BenchTupleBox<(BenchOpenItem, int)> OpenTuple = null!;
}

// Issue #6: a service log batch of 1,000 events.
/// <summary>Lists the log severities.</summary>
public enum BenchSeverity
{
    /// <summary>A debug event.</summary>
    Debug,
    /// <summary>An informational event.</summary>
    Info,
    /// <summary>A warning.</summary>
    Warning,
    /// <summary>An error.</summary>
    Error,
}

/// <summary>Holds one structured log event.</summary>
[LuminPackable]
public sealed partial class BenchLogEvent
{
    /// <summary>Gets or sets the timestamp.</summary>
    public DateTime Timestamp;
    /// <summary>Gets or sets the severity.</summary>
    public BenchSeverity Level;
    /// <summary>Gets or sets the template.</summary>
    public string Template = string.Empty;
    /// <summary>Gets or sets the trace identifier.</summary>
    public string TraceId = string.Empty;
    /// <summary>Gets or sets the elapsed time.</summary>
    public double ElapsedMs;
    /// <summary>Gets or sets the event tags.</summary>
    public Dictionary<string, string> Tags = [];
}

/// <summary>Holds one batch of structured log events.</summary>
[LuminPackable]
public sealed partial class BenchLogBatch
{
    /// <summary>Gets or sets the service name.</summary>
    public string Service = string.Empty;
    /// <summary>Gets or sets the host name.</summary>
    public string Host = string.Empty;
    /// <summary>Gets or sets the events.</summary>
    public List<BenchLogEvent> Events = [];
}

// Issue #6: an area-of-interest update of 200 events through an abstract base.
/// <summary>Provides the polymorphic area-of-interest event base.</summary>
[LuminPackable]
[LuminPackUnion(0, typeof(BenchEntityEnter))]
[LuminPackUnion(1, typeof(BenchEntityLeave))]
[LuminPackUnion(2, typeof(BenchEntityMove))]
[LuminPackUnion(3, typeof(BenchSkillCast))]
[LuminPackUnion(4, typeof(BenchBuffChanged))]
public abstract partial class BenchAoiEvent
{
    /// <summary>Gets or sets the entity identifier.</summary>
    public int EntityId;
}

/// <summary>Reports an entity entering the area.</summary>
[LuminPackable]
public sealed partial class BenchEntityEnter : BenchAoiEvent
{
    /// <summary>Gets or sets the entity type.</summary>
    public short Type;
    /// <summary>Gets or sets the name.</summary>
    public string Name = string.Empty;
    /// <summary>Gets or sets the position.</summary>
    public BenchVec3 Position;
    /// <summary>Gets or sets the level.</summary>
    public int Level;
    /// <summary>Gets or sets the guild identifier.</summary>
    public int? GuildId;
    /// <summary>Gets or sets the equipment identifiers.</summary>
    public List<int> Equipment = [];
}

/// <summary>Reports an entity leaving the area.</summary>
[LuminPackable]
public sealed partial class BenchEntityLeave : BenchAoiEvent
{
    /// <summary>Gets or sets the reason.</summary>
    public byte Reason;
}

/// <summary>Reports an entity moving inside the area.</summary>
[LuminPackable]
public sealed partial class BenchEntityMove : BenchAoiEvent
{
    /// <summary>Gets or sets the position.</summary>
    public BenchVec3 Position;
    /// <summary>Gets or sets the direction.</summary>
    public BenchVec3 Direction;
    /// <summary>Gets or sets the speed.</summary>
    public float Speed;
}

/// <summary>Holds one damage hit of a skill cast.</summary>
[LuminPackable]
public partial struct BenchDamageHit
{
    /// <summary>Gets or sets the target identifier.</summary>
    public int TargetId;
    /// <summary>Gets or sets the amount.</summary>
    public int Amount;
    /// <summary>Gets or sets whether the hit was critical.</summary>
    public bool Critical;
}

/// <summary>Reports a skill cast inside the area.</summary>
[LuminPackable]
public sealed partial class BenchSkillCast : BenchAoiEvent
{
    /// <summary>Gets or sets the skill identifier.</summary>
    public short SkillId;
    /// <summary>Gets or sets the target position.</summary>
    public BenchVec3 Target;
    /// <summary>Gets or sets the damage hits.</summary>
    public List<BenchDamageHit> Hits = [];
}

/// <summary>Reports a buff change inside the area.</summary>
[LuminPackable]
public sealed partial class BenchBuffChanged : BenchAoiEvent
{
    /// <summary>Gets or sets the buff identifier.</summary>
    public short BuffId;
    /// <summary>Gets or sets the stack count.</summary>
    public byte Stacks;
    /// <summary>Gets or sets the duration.</summary>
    public float Duration;
}

/// <summary>Holds one chat line.</summary>
[LuminPackable]
public sealed partial class BenchChatMessage
{
    /// <summary>Gets or sets the channel.</summary>
    public byte Channel;
    /// <summary>Gets or sets the sender identifier.</summary>
    public long SenderId;
    /// <summary>Gets or sets the sender name.</summary>
    public string SenderName = string.Empty;
    /// <summary>Gets or sets the text.</summary>
    public string Text = string.Empty;
    /// <summary>Gets or sets the send time.</summary>
    public DateTime SentAt;
}

/// <summary>Holds one area-of-interest update.</summary>
[LuminPackable]
public sealed partial class BenchAoiUpdate
{
    /// <summary>Gets or sets the server tick.</summary>
    public long ServerTick;
    /// <summary>Gets or sets the scene identifier.</summary>
    public int SceneId;
    /// <summary>Gets or sets the events.</summary>
    public List<BenchAoiEvent> Events = [];
    /// <summary>Gets or sets the chat lines.</summary>
    public List<BenchChatMessage> Chat = [];
}

// Issue #6: a world region with 4,096 tiles, shared alliances, cities and armies.
/// <summary>Holds one map tile.</summary>
[LuminPackable]
public partial struct BenchTile
{
    /// <summary>Gets or sets the terrain.</summary>
    public byte Terrain;
    /// <summary>Gets or sets the level.</summary>
    public byte Level;
    /// <summary>Gets or sets the owner identifier.</summary>
    public int OwnerId;
}

/// <summary>Holds one alliance shared by many cities and armies.</summary>
[LuminPackable(GeneratorType.CircleReference)]
public sealed partial class BenchAlliance
{
    /// <summary>Gets or sets the identifier.</summary>
    [LuminPackOrder(0)] public int Id;
    /// <summary>Gets or sets the name.</summary>
    [LuminPackOrder(1)] public string Name = string.Empty;
    /// <summary>Gets or sets the tag.</summary>
    [LuminPackOrder(2)] public string Tag = string.Empty;
    /// <summary>Gets or sets the member count.</summary>
    [LuminPackOrder(3)] public int Members;
    /// <summary>Gets or sets the power.</summary>
    [LuminPackOrder(4)] public long Power;
}

/// <summary>Holds one map point.</summary>
[LuminPackable]
public partial struct BenchMapPoint
{
    /// <summary>Gets or sets the column.</summary>
    public short X;
    /// <summary>Gets or sets the row.</summary>
    public short Y;
}

/// <summary>Lists the march kinds.</summary>
public enum BenchMarchKind
{
    /// <summary>An attack march.</summary>
    Attack,
    /// <summary>A gathering march.</summary>
    Gather,
    /// <summary>A reinforcement march.</summary>
    Reinforce,
    /// <summary>A scouting march.</summary>
    Scout,
}

/// <summary>Holds one army march.</summary>
[LuminPackable]
public sealed partial class BenchMarch
{
    /// <summary>Gets or sets the kind.</summary>
    public BenchMarchKind Kind;
    /// <summary>Gets or sets the departure time.</summary>
    public DateTime Departed;
    /// <summary>Gets or sets the arrival time.</summary>
    public DateTime Arrives;
    /// <summary>Gets or sets the path.</summary>
    public List<BenchMapPoint> Path = [];
}

/// <summary>Holds one city.</summary>
[LuminPackable]
public sealed partial class BenchCity
{
    /// <summary>Gets or sets the identifier.</summary>
    public int Id;
    /// <summary>Gets or sets the name.</summary>
    public string Name = string.Empty;
    /// <summary>Gets or sets the owner identifier.</summary>
    public int OwnerId;
    /// <summary>Gets or sets the position.</summary>
    public BenchMapPoint Position;
    /// <summary>Gets or sets the level.</summary>
    public byte Level;
    /// <summary>Gets or sets the buildings.</summary>
    public Dictionary<int, int> Buildings = [];
    /// <summary>Gets or sets the owning alliance.</summary>
    public BenchAlliance Alliance = null!;
}

/// <summary>Holds one army.</summary>
[LuminPackable]
public sealed partial class BenchArmy
{
    /// <summary>Gets or sets the identifier.</summary>
    public long Id;
    /// <summary>Gets or sets the owner identifier.</summary>
    public int OwnerId;
    /// <summary>Gets or sets the owning alliance.</summary>
    public BenchAlliance Alliance = null!;
    /// <summary>Gets or sets the troop counts.</summary>
    public Dictionary<int, int> Troops = [];
    /// <summary>Gets or sets the march.</summary>
    public BenchMarch March = null!;
}

/// <summary>Holds one region of the world map.</summary>
[LuminPackable]
public sealed partial class BenchWorldRegion
{
    /// <summary>Gets or sets the region column.</summary>
    public short RegionX;
    /// <summary>Gets or sets the region row.</summary>
    public short RegionY;
    /// <summary>Gets or sets the tiles.</summary>
    public BenchTile[] Tiles = [];
    /// <summary>Gets or sets the alliances.</summary>
    public List<BenchAlliance> Alliances = [];
    /// <summary>Gets or sets the cities.</summary>
    public List<BenchCity> Cities = [];
    /// <summary>Gets or sets the armies.</summary>
    public List<BenchArmy> Armies = [];
}

// Issue #6: an interface list inside each entity of a snapshot.
/// <summary>Provides the polymorphic component contract.</summary>
[LuminPackable]
[LuminPackUnion(0, typeof(BenchTransformComponent))]
[LuminPackUnion(1, typeof(BenchVelocityComponent))]
[LuminPackUnion(2, typeof(BenchHealthComponent))]
[LuminPackUnion(3, typeof(BenchInventoryComponent))]
[LuminPackUnion(4, typeof(BenchAiComponent))]
[LuminPackUnion(5, typeof(BenchColliderComponent))]
[LuminPackUnion(6, typeof(BenchRenderComponent))]
[LuminPackUnion(7, typeof(BenchAudioComponent))]
[LuminPackUnion(8, typeof(BenchScriptComponent))]
[LuminPackUnion(9, typeof(BenchNetworkComponent))]
[LuminPackUnion(10, typeof(BenchExtraComponent))]
public partial interface IBenchComponent
{
}

/// <summary>Provides the transform component.</summary>
[LuminPackable]
public sealed partial class BenchTransformComponent : IBenchComponent
{
    /// <summary>Gets or sets the position.</summary>
    public float X, Y, Z;
}

/// <summary>Provides the velocity component.</summary>
[LuminPackable]
public sealed partial class BenchVelocityComponent : IBenchComponent
{
    /// <summary>Gets or sets the velocity.</summary>
    public float X, Y, Z;
}

/// <summary>Provides the health component.</summary>
[LuminPackable]
public sealed partial class BenchHealthComponent : IBenchComponent
{
    /// <summary>Gets or sets the hit points.</summary>
    public int Hp;
}

/// <summary>Provides the inventory component.</summary>
[LuminPackable]
public sealed partial class BenchInventoryComponent : IBenchComponent
{
    /// <summary>Gets or sets the item identifiers.</summary>
    public List<int> Items = [];
}

/// <summary>Provides the AI component.</summary>
[LuminPackable]
public sealed partial class BenchAiComponent : IBenchComponent
{
    /// <summary>Gets or sets the state.</summary>
    public int State;
}

/// <summary>Provides the collider component.</summary>
[LuminPackable]
public sealed partial class BenchColliderComponent : IBenchComponent
{
    /// <summary>Gets or sets the radius.</summary>
    public float Radius;
}

/// <summary>Provides the render component.</summary>
[LuminPackable]
public sealed partial class BenchRenderComponent : IBenchComponent
{
    /// <summary>Gets or sets the mesh identifier.</summary>
    public int MeshId;
}

/// <summary>Provides the audio component.</summary>
[LuminPackable]
public sealed partial class BenchAudioComponent : IBenchComponent
{
    /// <summary>Gets or sets the clip identifier.</summary>
    public int Clip;
}

/// <summary>Provides the script component.</summary>
[LuminPackable]
public sealed partial class BenchScriptComponent : IBenchComponent
{
    /// <summary>Gets or sets the script name.</summary>
    public string Name = string.Empty;
}

/// <summary>Provides the network component.</summary>
[LuminPackable]
public sealed partial class BenchNetworkComponent : IBenchComponent
{
    /// <summary>Gets or sets the owner identifier.</summary>
    public int Owner;
}

/// <summary>Provides the extra component, the last Union case.</summary>
[LuminPackable]
public sealed partial class BenchExtraComponent : IBenchComponent
{
    /// <summary>Gets or sets the tag.</summary>
    public int Tag;
}

/// <summary>Holds one entity of a snapshot.</summary>
[LuminPackable]
public sealed partial class BenchEntity
{
    /// <summary>Gets or sets the identifier.</summary>
    public int Id;
    /// <summary>Gets or sets the components.</summary>
    public List<IBenchComponent> Components = [];
}

/// <summary>Holds one snapshot of entities.</summary>
[LuminPackable]
public sealed partial class BenchSnapshot
{
    /// <summary>Gets or sets the entities.</summary>
    public List<BenchEntity> Entities = [];
}

// Issue #6: arrays of structs with nullable members, and nullable collections.
/// <summary>Holds the nullable members of one element.</summary>
[LuminPackable]
public partial struct BenchNullableFields
{
    /// <summary>Gets or sets the integer.</summary>
    public int? Int;
    /// <summary>Gets or sets the long integer.</summary>
    public long? Long;
    /// <summary>Gets or sets the time.</summary>
    public DateTime? Time;
    /// <summary>Gets or sets the identifier.</summary>
    public Guid? Id;
    /// <summary>Gets or sets the flag.</summary>
    public bool? Flag;
    /// <summary>Gets or sets the value.</summary>
    public double? Value;
}

/// <summary>Holds the array of nullable structs.</summary>
[LuminPackable]
public sealed partial class BenchNullableFieldsBatch
{
    /// <summary>Gets or sets the elements.</summary>
    public BenchNullableFields[] Items = [];
}

/// <summary>Holds the nullable collection shapes.</summary>
[LuminPackable]
public sealed partial class BenchNullableCollections
{
    /// <summary>Gets or sets the set.</summary>
    public HashSet<int?> Set = [];
    /// <summary>Gets or sets the linked list.</summary>
    public LinkedList<int?> Linked = new();
    /// <summary>Gets or sets the collection.</summary>
    public Collection<int?> Collection = [];
    /// <summary>Gets or sets the concurrent queue.</summary>
    public ConcurrentQueue<int?> Concurrent = new();
    /// <summary>Gets or sets the queue.</summary>
    public Queue<int?> Queue = new();
    /// <summary>Gets or sets the list interface.</summary>
    public IList<int?> List = new List<int?>();
    /// <summary>Gets or sets the memory block.</summary>
    public Memory<int?> Memory;
    /// <summary>Gets or sets the grid.</summary>
    public int?[,] Grid = new int?[0, 0];
}

/// <summary>Holds one vertex.</summary>
[LuminPackable]
public partial struct BenchVec3
{
    /// <summary>Gets or sets the first component.</summary>
    public float X;
    /// <summary>Gets or sets the second component.</summary>
    public float Y;
    /// <summary>Gets or sets the third component.</summary>
    public float Z;
}

/// <summary>Holds one mesh of vertices and indices.</summary>
[LuminPackable]
public sealed partial class BenchMesh
{
    /// <summary>Gets or sets the vertices.</summary>
    public BenchVec3[] Vertices = [];
    /// <summary>Gets or sets the indices.</summary>
    public int[] Indices = [];
}

/// <summary>Holds the meshes of one application model.</summary>
[LuminPackable]
public sealed partial class BenchMeshBatch
{
    /// <summary>Gets or sets the meshes.</summary>
    public BenchMesh[] Items = [];
}

// Issue #7: the previous release's bytes read by the next one.
/// <summary>Holds the cached profile of the previous release.</summary>
[LuminPackable(GeneratorType.VersionTolerant)]
public sealed partial class BenchProfileV1
{
    /// <summary>Gets or sets the identifier.</summary>
    [LuminPackOrder(0)] public long Id;
    /// <summary>Gets or sets the user name.</summary>
    [LuminPackOrder(1)] public string UserName = string.Empty;
    /// <summary>Gets or sets the email address.</summary>
    [LuminPackOrder(2)] public string Email = string.Empty;
    /// <summary>Gets or sets the display name.</summary>
    [LuminPackOrder(3)] public string DisplayName = string.Empty;
    /// <summary>Gets or sets the avatar address that the next release removed.</summary>
    [LuminPackOrder(4)] public string AvatarUrl = string.Empty;
    /// <summary>Gets or sets the biography.</summary>
    [LuminPackOrder(5)] public string Bio = string.Empty;
    /// <summary>Gets or sets the creation time.</summary>
    [LuminPackOrder(6)] public DateTime CreatedAt;
    /// <summary>Gets or sets the last login time.</summary>
    [LuminPackOrder(7)] public DateTime? LastLoginAt;
    /// <summary>Gets or sets whether the email is verified.</summary>
    [LuminPackOrder(8)] public bool EmailVerified;
    /// <summary>Gets or sets the birth year.</summary>
    [LuminPackOrder(9)] public int? BirthYear;
    /// <summary>Gets or sets the country.</summary>
    [LuminPackOrder(10)] public string Country = string.Empty;
    /// <summary>Gets or sets the time zone.</summary>
    [LuminPackOrder(11)] public string TimeZone = string.Empty;
    /// <summary>Gets or sets the follower count.</summary>
    [LuminPackOrder(12)] public int Followers;
    /// <summary>Gets or sets the roles.</summary>
    [LuminPackOrder(13)] public List<string> Roles = [];
    /// <summary>Gets or sets the preferences.</summary>
    [LuminPackOrder(14)] public Dictionary<string, string> Preferences = [];
}

/// <summary>Holds the cached profile of the next release.</summary>
[LuminPackable(GeneratorType.VersionTolerant)]
public sealed partial class BenchProfileV2
{
    /// <summary>Gets or sets the identifier.</summary>
    [LuminPackOrder(0)] public long Id;
    /// <summary>Gets or sets the user name.</summary>
    [LuminPackOrder(1)] public string UserName = string.Empty;
    /// <summary>Gets or sets the email address.</summary>
    [LuminPackOrder(2)] public string Email = string.Empty;
    /// <summary>Gets or sets the display name.</summary>
    [LuminPackOrder(3)] public string DisplayName = string.Empty;
    /// <summary>Gets or sets the biography.</summary>
    [LuminPackOrder(5)] public string Bio = string.Empty;
    /// <summary>Gets or sets the creation time.</summary>
    [LuminPackOrder(6)] public DateTime CreatedAt;
    /// <summary>Gets or sets the last login time.</summary>
    [LuminPackOrder(7)] public DateTime? LastLoginAt;
    /// <summary>Gets or sets whether the email is verified.</summary>
    [LuminPackOrder(8)] public bool EmailVerified;
    /// <summary>Gets or sets the birth year.</summary>
    [LuminPackOrder(9)] public int? BirthYear;
    /// <summary>Gets or sets the country.</summary>
    [LuminPackOrder(10)] public string Country = string.Empty;
    /// <summary>Gets or sets the time zone.</summary>
    [LuminPackOrder(11)] public string TimeZone = string.Empty;
    /// <summary>Gets or sets the follower count.</summary>
    [LuminPackOrder(12)] public int Followers;
    /// <summary>Gets or sets the roles.</summary>
    [LuminPackOrder(13)] public List<string> Roles = [];
    /// <summary>Gets or sets the preferences.</summary>
    [LuminPackOrder(14)] public Dictionary<string, string> Preferences = [];
    /// <summary>Gets or sets the pronouns added by the next release.</summary>
    [LuminPackOrder(15)] public string Pronouns = string.Empty;
    /// <summary>Gets or sets the badges added by the next release.</summary>
    [LuminPackOrder(16)] public List<string> Badges = [];
}

/// <summary>Holds one idle-game building.</summary>
[LuminPackable]
public sealed partial class BenchBuilding
{
    /// <summary>Gets or sets the identifier.</summary>
    public short Id;
    /// <summary>Gets or sets the level.</summary>
    public int Level;
    /// <summary>Gets or sets the multiplier.</summary>
    public double Multiplier;
    /// <summary>Gets or sets the upgrade end time.</summary>
    public DateTime? UpgradeEndsAt;
}

/// <summary>Holds the offline progress of an idle-game save.</summary>
[LuminPackable]
public sealed partial class BenchOfflineState
{
    /// <summary>Gets or sets the last seen time.</summary>
    public DateTime LastSeen;
    /// <summary>Gets or sets the production rate.</summary>
    public double RatePerSecond;
    /// <summary>Gets or sets the capped hours.</summary>
    public double CapHours;
    /// <summary>Gets or sets the pending amount.</summary>
    public double Pending;
}

/// <summary>Holds the idle-game save of the previous release.</summary>
[LuminPackable(GeneratorType.VersionTolerant)]
public sealed partial class BenchIdleSaveV1
{
    /// <summary>Gets or sets the player identifier.</summary>
    [LuminPackOrder(0)] public long PlayerId;
    /// <summary>Gets or sets the save time.</summary>
    [LuminPackOrder(1)] public DateTime SavedAt;
    /// <summary>Gets or sets the gold.</summary>
    [LuminPackOrder(2)] public double Gold;
    /// <summary>Gets or sets the gems.</summary>
    [LuminPackOrder(3)] public double Gems;
    /// <summary>Gets or sets the lifetime earnings.</summary>
    [LuminPackOrder(4)] public double LifetimeEarnings;
    /// <summary>Gets or sets the resources.</summary>
    [LuminPackOrder(5)] public Dictionary<int, double> Resources = [];
    /// <summary>Gets or sets the buildings.</summary>
    [LuminPackOrder(6)] public List<BenchBuilding> Buildings = [];
    /// <summary>Gets or sets the upgrades.</summary>
    [LuminPackOrder(7)] public Dictionary<int, byte> Upgrades = [];
    /// <summary>Gets or sets the offline state.</summary>
    [LuminPackOrder(8)] public BenchOfflineState Offline = null!;
    /// <summary>Gets or sets the statistics.</summary>
    [LuminPackOrder(9)] public Dictionary<int, long> Stats = [];
    /// <summary>Gets or sets the achievements.</summary>
    [LuminPackOrder(10)] public List<int> Achievements = [];
}

/// <summary>Holds the idle-game save of the next release.</summary>
[LuminPackable(GeneratorType.VersionTolerant)]
public sealed partial class BenchIdleSaveV2
{
    /// <summary>Gets or sets the player identifier.</summary>
    [LuminPackOrder(0)] public long PlayerId;
    /// <summary>Gets or sets the save time.</summary>
    [LuminPackOrder(1)] public DateTime SavedAt;
    /// <summary>Gets or sets the gold.</summary>
    [LuminPackOrder(2)] public double Gold;
    /// <summary>Gets or sets the gems.</summary>
    [LuminPackOrder(3)] public double Gems;
    /// <summary>Gets or sets the lifetime earnings.</summary>
    [LuminPackOrder(4)] public double LifetimeEarnings;
    /// <summary>Gets or sets the resources.</summary>
    [LuminPackOrder(5)] public Dictionary<int, double> Resources = [];
    /// <summary>Gets or sets the buildings.</summary>
    [LuminPackOrder(6)] public List<BenchBuilding> Buildings = [];
    /// <summary>Gets or sets the upgrades.</summary>
    [LuminPackOrder(7)] public Dictionary<int, byte> Upgrades = [];
    /// <summary>Gets or sets the offline state.</summary>
    [LuminPackOrder(8)] public BenchOfflineState Offline = null!;
    /// <summary>Gets or sets the statistics.</summary>
    [LuminPackOrder(9)] public Dictionary<int, long> Stats = [];
    /// <summary>Gets or sets the achievements.</summary>
    [LuminPackOrder(10)] public List<int> Achievements = [];
    /// <summary>Gets or sets the prestige count added by the next release.</summary>
    [LuminPackOrder(11)] public int PrestigeCount;
    /// <summary>Gets or sets the prestige multiplier added by the next release.</summary>
    [LuminPackOrder(12)] public double PrestigeMultiplier;
}

// Issue #8: a polymorphic list whose writer has to grow.
/// <summary>Provides the polymorphic shape base.</summary>
[LuminPackable]
[LuminPackUnion(0, typeof(BenchCircle))]
[LuminPackUnion(1, typeof(BenchRectangle))]
public abstract partial class BenchShape
{
    /// <summary>Gets or sets the color.</summary>
    public int Color;
}

/// <summary>Provides a circular shape.</summary>
[LuminPackable]
public sealed partial class BenchCircle : BenchShape
{
    /// <summary>Gets or sets the radius.</summary>
    public float Radius;
}

/// <summary>Provides a rectangular shape.</summary>
[LuminPackable]
public sealed partial class BenchRectangle : BenchShape
{
    /// <summary>Gets or sets the width.</summary>
    public float Width;
    /// <summary>Gets or sets the height.</summary>
    public float Height;
}

// A type derived from List<T> of itself used to be treated as an object without members.
/// <summary>Holds a tree whose children are its own collection entries.</summary>
[LuminPackable]
public sealed partial class BenchTree : List<BenchTree>
{
}

/// <summary>Protects the round trips that the 1.x benchmark fixtures reported as failing or corrupting data.</summary>
public sealed class BenchmarkIssueRegressionTest
{
    /// <summary>Reads a value back through the binary format.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="value">The value to write.</param>
    /// <returns>The restored value.</returns>
    private static T RoundTrip<T>(T value)
    {
        T restored = default!;
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(value), ref restored);
        return restored;
    }

    /// <summary>Round-trips a named value tuple beside plain members.</summary>
    [Fact]
    public void ValueTupleMemberRoundTrips()
    {
        var value = new BenchWaypoint { X = 1.5f, Y = -2.25f, Stamp = (123, 7), Visited = true };

        var restored = RoundTrip(value);

        Assert.Equal(1.5f, restored.X);
        Assert.Equal(-2.25f, restored.Y);
        Assert.Equal((123, 7), restored.Stamp);
        Assert.True(restored.Visited);
    }

    /// <summary>Round-trips a type derived from a list of that same type in both formats.</summary>
    [Fact]
    public void ListDerivedSelfReferenceRoundTrips()
    {
        var value = new BenchTree();
        for (int branch = 0; branch < 3; branch++)
        {
            var child = new BenchTree();
            for (int leaf = 0; leaf < 2; leaf++)
                child.Add(new BenchTree());
            value.Add(child);
        }

        var restored = RoundTrip(value);

        Assert.Equal(3, restored.Count);
        Assert.Equal(2, restored[2].Count);
        Assert.Equal(10, Count(restored));

        BenchTree fromJson = null!;
        LuminPackSerializer.DeserializeJson(LuminPackSerializer.SerializeJson(value), ref fromJson);

        Assert.Equal(10, Count(fromJson));

        static int Count(BenchTree tree)
        {
            int count = 1;
            foreach (var child in tree)
                count += Count(child);
            return count;
        }
    }

    /// <summary>Round-trips object lists nested inside object lists.</summary>
    [Fact]
    public void NestedObjectListsRoundTrip()
    {
        var value = new BenchPage
        {
            HasMore = true,
            Number = 2,
            Items = Enumerable.Range(0, 3).Select(index => new BenchQuestion
            {
                QuestionId = index,
                Title = "q" + index,
                Comments =
                [
                    new BenchComment { CommentId = index * 10, Body = "c0" },
                    new BenchComment { CommentId = index * 10 + 1, Body = "c1" },
                ],
                Answers =
                [
                    new BenchAnswer
                    {
                        AnswerId = index * 100,
                        Comments =
                        [
                            new BenchComment { CommentId = 0, Body = "a0" },
                            new BenchComment { CommentId = 1, Body = "a1" },
                        ],
                    },
                ],
            }).ToList(),
        };

        var restored = RoundTrip(value);

        Assert.True(restored.HasMore);
        Assert.Equal(2, restored.Number);
        Assert.Equal(3, restored.Items.Count);
        Assert.Equal("q2", restored.Items[2].Title);
        Assert.Equal("c1", restored.Items[2].Comments[1].Body);
        Assert.Equal(200, restored.Items[2].Answers[0].AnswerId);
        Assert.Equal("a1", restored.Items[2].Answers[0].Comments[1].Body);
    }

    /// <summary>Round-trips members typed as a generic base closed over each tuple element shape.</summary>
    [Fact]
    public void AnnotatedClosedGenericTupleCasesRoundTrip()
    {
        var value = new BenchTupleBatch
        {
            Tuple = new BenchTupleIntCase { Value = (7, 9L) },
            StringTuple = new BenchTupleStringCase { Value = (8, "s") },
            SealedTuple = new BenchTupleSealedCase { Value = (new BenchSealedItem { X = 1, Y = 2 }, 3) },
            OpenTuple = new BenchTupleOpenCase { Value = (new BenchOpenItem { X = 4, Y = 5 }, 6) },
        };

        var restored = RoundTrip(value);

        Assert.Equal((7, 9L), Assert.IsType<BenchTupleIntCase>(restored.Tuple).Value);
        Assert.Equal((8, "s"), Assert.IsType<BenchTupleStringCase>(restored.StringTuple).Value);
        var sealedCase = Assert.IsType<BenchTupleSealedCase>(restored.SealedTuple);
        Assert.Equal(1, sealedCase.Value.Item1.X);
        Assert.Equal(2, sealedCase.Value.Item1.Y);
        Assert.Equal(3, sealedCase.Value.Item2);
        var openCase = Assert.IsType<BenchTupleOpenCase>(restored.OpenTuple);
        Assert.Equal(4, openCase.Value.Item1.X);
        Assert.Equal(5, openCase.Value.Item1.Y);
        Assert.Equal(6, openCase.Value.Item2);
    }

    /// <summary>Round-trips a service log batch of 1,000 events and an area-of-interest update of 200 events.</summary>
    [Fact]
    public void ServiceLogAndAoiWorkloadsRoundTrip()
    {
        var log = new BenchLogBatch
        {
            Service = "checkout-api",
            Host = "checkout-api-7d9f8c6b5-x2kqp",
            Events = Enumerable.Range(0, 1000).Select(index => new BenchLogEvent
            {
                Timestamp = new DateTime(2026, 1, 1).AddMilliseconds(index * 37),
                Level = (BenchSeverity)(index % 4),
                Template = "HTTP {Method} {Path} responded {StatusCode}",
                TraceId = (index * 0x9e3779b9L).ToString("x16") + (index * 0x85ebca6bL).ToString("x16"),
                ElapsedMs = index * 0.25,
                Tags = new Dictionary<string, string> { ["path"] = "/api/orders", ["status"] = "200" },
            }).ToList(),
        };

        var restoredLog = RoundTrip(log);

        Assert.Equal(1000, restoredLog.Events.Count);
        Assert.Equal(log.Events[999].Timestamp, restoredLog.Events[999].Timestamp);
        Assert.Equal(log.Events[999].TraceId, restoredLog.Events[999].TraceId);
        Assert.Equal(BenchSeverity.Error, restoredLog.Events[999].Level);
        Assert.Equal("200", restoredLog.Events[999].Tags["status"]);

        var events = new List<BenchAoiEvent>(200);
        for (int index = 0; index < 200; index++)
        {
            events.Add((index % 10) switch
            {
                0 => new BenchEntityEnter
                {
                    EntityId = 50_000 + index,
                    Type = (short)(index % 30),
                    Name = "n" + index,
                    Position = new BenchVec3 { X = index, Y = 1, Z = 2 },
                    Level = index % 60,
                    GuildId = index % 3 == 0 ? null : index,
                    Equipment = [1, 2, 3, 4, 5, 6],
                },
                1 => new BenchEntityLeave { EntityId = 50_000 + index, Reason = (byte)(index % 3) },
                < 7 => new BenchEntityMove
                {
                    EntityId = 50_000 + index,
                    Position = new BenchVec3 { X = index, Y = index, Z = index },
                    Direction = new BenchVec3 { X = 1, Y = 0, Z = 0 },
                    Speed = index * 0.5f,
                },
                < 9 => new BenchSkillCast
                {
                    EntityId = 50_000 + index,
                    SkillId = (short)(index % 200),
                    Target = new BenchVec3 { X = index, Y = 2, Z = 3 },
                    Hits = Enumerable.Range(0, 1 + index % 5).Select(hit => new BenchDamageHit
                    {
                        TargetId = hit,
                        Amount = hit * 10,
                        Critical = hit % 5 == 0,
                    }).ToList(),
                },
                _ => new BenchBuffChanged
                {
                    EntityId = 50_000 + index,
                    BuffId = (short)(index % 300),
                    Stacks = (byte)(index % 6),
                    Duration = index * 0.75f,
                },
            });
        }

        var update = new BenchAoiUpdate
        {
            ServerTick = 9_200_311,
            SceneId = 1_002,
            Events = events,
            Chat = Enumerable.Range(0, 5).Select(index => new BenchChatMessage
            {
                Channel = (byte)index,
                SenderId = index,
                SenderName = "p" + index,
                Text = "t" + index,
                SentAt = new DateTime(2026, 2, 3).AddSeconds(index),
            }).ToList(),
        };

        var restoredUpdate = RoundTrip(update);

        Assert.Equal(200, restoredUpdate.Events.Count);
        Assert.Equal(5, restoredUpdate.Chat.Count);
        var enter = Assert.IsType<BenchEntityEnter>(restoredUpdate.Events[0]);
        Assert.Equal(50_000, enter.EntityId);
        Assert.Null(enter.GuildId);
        Assert.Equal(6, enter.Equipment.Count);
        var cast = Assert.IsType<BenchSkillCast>(restoredUpdate.Events[8]);
        Assert.Equal(50_008, cast.EntityId);
        Assert.Equal(4, cast.Hits.Count);
        Assert.Equal(8, cast.Target.X);
        var buff = Assert.IsType<BenchBuffChanged>(restoredUpdate.Events[19]);
        Assert.Equal(14.25f, buff.Duration);
        Assert.Equal(update.Chat[4].SentAt, restoredUpdate.Chat[4].SentAt);
    }

    /// <summary>Round-trips a world region with 4,096 tiles and alliances shared by cities and armies.</summary>
    [Fact]
    public void WorldRegionRoundTrips()
    {
        var alliances = Enumerable.Range(0, 6).Select(index => new BenchAlliance
        {
            Id = 700 + index,
            Name = "Alliance " + (char)('A' + index),
            Tag = "AL" + index,
            Members = 20 + index,
            Power = 1_000_000 + index,
        }).ToList();
        BenchMapPoint Point(int seed) => new() { X = (short)(seed % 64), Y = (short)(seed / 64 % 64) };
        var value = new BenchWorldRegion
        {
            RegionX = 12,
            RegionY = 7,
            Alliances = alliances,
            Tiles = Enumerable.Range(0, 64 * 64).Select(index => new BenchTile
            {
                Terrain = (byte)(index % 6),
                Level = (byte)(1 + index % 8),
                OwnerId = index % 4 == 0 ? index : 0,
            }).ToArray(),
            Cities = Enumerable.Range(0, 40).Select(index => new BenchCity
            {
                Id = 30_000 + index,
                Name = "City " + index,
                OwnerId = index * 3,
                Position = Point(index),
                Level = (byte)(1 + index % 25),
                Buildings = Enumerable.Range(0, 8).ToDictionary(building => 100 + building, building => building + 1),
                Alliance = alliances[index % 6],
            }).ToList(),
            Armies = Enumerable.Range(0, 120).Select(index => new BenchArmy
            {
                Id = 8_000_000 + index,
                OwnerId = index * 7,
                Alliance = alliances[index % 6],
                Troops = Enumerable.Range(0, 4).ToDictionary(troop => troop + 1, troop => 100 + troop),
                March = index % 3 == 0 ? null! : new BenchMarch
                {
                    Kind = (BenchMarchKind)(index % 4),
                    Departed = new DateTime(2026, 3, 1),
                    Arrives = new DateTime(2026, 3, 2),
                    Path = Enumerable.Range(0, 8 + index % 22).Select(step => Point(index + step)).ToList(),
                },
            }).ToList(),
        };

        var restored = RoundTrip(value);

        Assert.Equal(4096, restored.Tiles.Length);
        Assert.Equal(6, restored.Alliances.Count);
        Assert.Equal(40, restored.Cities.Count);
        Assert.Equal(120, restored.Armies.Count);
        Assert.Equal(value.Tiles[4095].OwnerId, restored.Tiles[4095].OwnerId);
        Assert.Equal(value.Tiles[4095].Terrain, restored.Tiles[4095].Terrain);
        Assert.Equal(8, restored.Cities[39].Buildings[107]);
        Assert.Equal(value.Cities[39].Position.X, restored.Cities[39].Position.X);
        Assert.Equal(103, restored.Armies[119].Troops[4]);
        Assert.Equal(value.Armies[119].March.Path.Count, restored.Armies[119].March.Path.Count);
        Assert.Contains(restored.Alliances, alliance => ReferenceEquals(alliance, restored.Cities[0].Alliance));
    }

    /// <summary>Round-trips arrays of meshes, arrays of nullable structs, nullable collections and component lists.</summary>
    [Fact]
    public void StructAndNullableCollectionShapesRoundTrip()
    {
        var meshes = Enumerable.Range(0, 20).Select(index => new BenchMesh
        {
            Vertices = Enumerable.Range(0, 500).Select(vertex => new BenchVec3
            {
                X = index,
                Y = vertex,
                Z = vertex * 0.5f,
            }).ToArray(),
            Indices = Enumerable.Range(0, 1500).Select(index2 => (index2 * (index + 3)) % 500).ToArray(),
        }).ToArray();

        var restoredMeshes = RoundTrip(new BenchMeshBatch { Items = meshes }).Items;

        Assert.Equal(20, restoredMeshes.Length);
        for (int mesh = 0; mesh < meshes.Length; mesh++)
        {
            Assert.Equal(500, restoredMeshes[mesh].Vertices.Length);
            Assert.Equal(1500, restoredMeshes[mesh].Indices.Length);
            for (int vertex = 0; vertex < 500; vertex++)
                Assert.Equal(meshes[mesh].Vertices[vertex], restoredMeshes[mesh].Vertices[vertex]);
            for (int index = 0; index < 1500; index++)
                Assert.Equal(meshes[mesh].Indices[index], restoredMeshes[mesh].Indices[index]);
        }

        var nullable = new BenchNullableFieldsBatch
        {
            Items = Enumerable.Range(0, 100).Select(index => (index % 3) switch
            {
                0 => new BenchNullableFields
                {
                    Int = index,
                    Long = index,
                    Time = new DateTime(2026, 1, 1).AddDays(index),
                    Id = Guid.NewGuid(),
                    Flag = true,
                    Value = index * 0.5,
                },
                1 => new BenchNullableFields { Int = null, Long = null, Time = null, Id = null, Flag = null, Value = null },
                _ => new BenchNullableFields { Int = -index, Long = null, Time = null, Id = null, Flag = false, Value = null },
            }).ToArray(),
        };

        var restoredNullable = RoundTrip(nullable).Items;

        Assert.Equal(100, restoredNullable.Length);
        Assert.Equal(0, restoredNullable[0].Int);
        Assert.True(restoredNullable[0].Flag);
        Assert.Equal(0L, restoredNullable[0].Long);
        Assert.Null(restoredNullable[1].Int);
        Assert.Null(restoredNullable[1].Id);
        Assert.Equal(-2, restoredNullable[2].Int);
        Assert.False(restoredNullable[2].Flag);
        Assert.Equal(nullable.Items[99].Value, restoredNullable[99].Value);

        var collections = new BenchNullableCollections
        {
            Set = [1, null, 3],
            Linked = new LinkedList<int?>([null, 2, null]),
            Collection = [4, null, 6],
            Concurrent = new ConcurrentQueue<int?>([7, null]),
            Queue = new Queue<int?>([8, null]),
            List = new List<int?> { 9, null },
            Memory = new int?[] { null, 4 }.AsMemory(),
            Grid = new int?[,] { { 1, null }, { null, 4 } },
        };

        var restoredCollections = RoundTrip(collections);

        Assert.True(restoredCollections.Set.SetEquals([1, null, 3]));
        Assert.Equal(new int?[] { null, 2, null }, restoredCollections.Linked);
        Assert.Equal(new int?[] { 4, null, 6 }, restoredCollections.Collection);
        Assert.Equal(new int?[] { 7, null }, restoredCollections.Concurrent);
        Assert.Equal(new int?[] { 8, null }, restoredCollections.Queue);
        Assert.Equal(new int?[] { 9, null }, restoredCollections.List);
        Assert.Null(restoredCollections.Memory.Span[0]);
        Assert.Equal(4, restoredCollections.Memory.Span[1]);
        Assert.Equal(2, restoredCollections.Grid.GetLength(0));
        Assert.Equal(2, restoredCollections.Grid.GetLength(1));
        Assert.Equal(4, restoredCollections.Grid[1, 1]);
        Assert.Null(restoredCollections.Grid[0, 1]);

        var snapshot = new BenchSnapshot
        {
            Entities = Enumerable.Range(0, 5).Select(entity => new BenchEntity
            {
                Id = entity,
                Components = Enumerable.Range(0, 11).Select(component => component switch
                {
                    0 => (IBenchComponent)new BenchTransformComponent { X = entity, Y = 2, Z = 3 },
                    1 => new BenchVelocityComponent { X = 1, Y = entity, Z = 0 },
                    2 => new BenchHealthComponent { Hp = 100 + entity },
                    3 => new BenchInventoryComponent { Items = [entity, entity + 1] },
                    4 => new BenchAiComponent { State = entity },
                    5 => new BenchColliderComponent { Radius = entity + 0.5f },
                    6 => new BenchRenderComponent { MeshId = entity },
                    7 => new BenchAudioComponent { Clip = entity },
                    8 => new BenchScriptComponent { Name = "s" + entity },
                    9 => new BenchNetworkComponent { Owner = entity },
                    _ => new BenchExtraComponent { Tag = entity * 10 },
                }).ToList(),
            }).ToList(),
        };

        var restoredSnapshot = RoundTrip(snapshot);

        Assert.Equal(5, restoredSnapshot.Entities.Count);
        Assert.Equal(11, Assert.Single(restoredSnapshot.Entities, entity => entity.Id == 4).Components.Count);
        Assert.Equal(40, Assert.IsType<BenchExtraComponent>(restoredSnapshot.Entities[4].Components[10]).Tag);
        Assert.Equal("s2", Assert.IsType<BenchScriptComponent>(restoredSnapshot.Entities[2].Components[8]).Name);
        Assert.Equal(2, Assert.IsType<BenchHealthComponent>(restoredSnapshot.Entities[2].Components[2]).Hp - 100);
    }

    /// <summary>Uses one headerless nullable layout across collections in the default Tagged mode.</summary>
    [Fact]
    public void NullableCollectionShapesUseContiguousBlocks()
    {
        int?[] elements = [8, null, -5, 2];
        byte[] native = LuminPackSerializer.Serialize(elements);
        var list = new List<int?>(elements);
        var wrapper = new ReadOnlyCollection<int?>(list);
        Assert.Equal(native, LuminPackSerializer.Serialize(list));
        Assert.Equal(native, LuminPackSerializer.Serialize(wrapper));
        Assert.Equal(native, LuminPackSerializer.Serialize(new Collection<int?>(list)));
        Assert.Equal(native, LuminPackSerializer.Serialize<IList<int?>>(list));
        Assert.Equal(native, LuminPackSerializer.Serialize<IReadOnlyList<int?>>(wrapper));

        var queue = new Queue<int?>(4);
        queue.Enqueue(100);
        queue.Enqueue(200);
        queue.Enqueue(elements[0]);
        queue.Enqueue(elements[1]);
        queue.Dequeue();
        queue.Dequeue();
        queue.Enqueue(elements[2]);
        queue.Enqueue(elements[3]);
        byte[] queueBytes = LuminPackSerializer.Serialize(queue);
        Assert.Equal(native[..sizeof(int)], queueBytes[..sizeof(int)]);
        Assert.Equal(new byte[2 * sizeof(int)], queueBytes[sizeof(int)..(3 * sizeof(int))]);
        Assert.Equal(native, queueBytes[(3 * sizeof(int))..]);

        var set = new HashSet<int?> { 8, 100, null, -5, 2 };
        Assert.True(set.Remove(100));
        Assert.Equal(LuminPackSerializer.Serialize(set.ToArray()), LuminPackSerializer.Serialize(set));

        int?[,] matrix = { { elements[0], elements[1] }, { elements[2], elements[3] } };
        byte[] matrixBytes = LuminPackSerializer.Serialize(matrix);
        Assert.Equal(native[sizeof(int)..], matrixBytes[(1 + 3 * sizeof(int))..]);
        Assert.Equal(elements, RoundTrip(matrix).Cast<int?>());
    }

    /// <summary>Round-trips a large bit array without faulting on read.</summary>
    [Fact]
    public void LargeBitArrayRoundTrips()
    {
        var small = new BitArray([true, false, true, true, false, false, true, false, true]);
        var restoredSmall = RoundTrip(small);
        Assert.Equal(small.Length, restoredSmall.Length);
        for (int bit = 0; bit < small.Length; bit++)
            Assert.Equal(small[bit], restoredSmall[bit]);

        var large = new BitArray(100_003);
        for (int bit = 0; bit < large.Length; bit++)
            large[bit] = bit % 7919 == 0;
        var restoredLarge = RoundTrip(large);
        Assert.Equal(large.Length, restoredLarge.Length);
        for (int bit = 0; bit < large.Length; bit++)
            Assert.Equal(large[bit], restoredLarge[bit]);
    }

    /// <summary>Writes 1,000 polymorphic elements into a writer that has to grow.</summary>
    [Fact]
    public void PolymorphicListGrowsWriter()
    {
        var value = Enumerable.Range(0, 1000).Select(index => index % 2 == 0
            ? (BenchShape)new BenchCircle { Color = index, Radius = index }
            : new BenchRectangle { Color = index, Width = index, Height = index + 1 }).ToList();

        using var buffer = new LuminBufferWriter(true);
        LuminPackSerializer.Serialize(value, buffer);

        List<BenchShape> restored = null!;
        LuminPackSerializer.Deserialize(buffer.GetSpan(), ref restored);
        Assert.Equal(1000, restored.Count);
        Assert.Equal(500, restored.OfType<BenchCircle>().Count());
        Assert.Equal(500, restored.OfType<BenchRectangle>().Count());
        Assert.Equal(998, restored[998].Color);
        Assert.Equal(999f, Assert.IsType<BenchRectangle>(restored[999]).Width);
        Assert.Equal(998f, Assert.IsType<BenchCircle>(restored[998]).Radius);
    }

    /// <summary>Reads the previous profile bytes as the next profile and back.</summary>
    [Fact]
    public void ProfileCacheVersionToleranceRoundTrips()
    {
        var previous = new BenchProfileV1
        {
            Id = 48_213,
            UserName = "jdoe48213",
            Email = "jane@example.com",
            DisplayName = "Jane Doe",
            AvatarUrl = "https://cdn.example.com/avatars/48213/256.png",
            Bio = null!,
            CreatedAt = new DateTime(2020, 1, 2),
            LastLoginAt = new DateTime(2026, 5, 6),
            EmailVerified = true,
            BirthYear = null,
            Country = "US",
            TimeZone = "America/Los_Angeles",
            Followers = 1234,
            Roles = ["member", "beta-tester"],
            Preferences = new Dictionary<string, string> { ["theme"] = "dark", ["language"] = "en" },
        };

        BenchProfileV2 next = null!;
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(previous), ref next);

        Assert.Equal(48_213, next.Id);
        Assert.Equal("jdoe48213", next.UserName);
        Assert.Null(next.Bio);
        Assert.Equal(1234, next.Followers);
        Assert.Equal(previous.Roles, next.Roles);
        Assert.Equal("dark", next.Preferences["theme"]);
        // Members appended after the bytes were written have no stored value and read back as null.
        Assert.Null(next.Pronouns);
        Assert.Null(next.Badges);

        next.Pronouns = "she/her";
        next.Badges = ["early-adopter", "top-reviewer"];
        BenchProfileV1 restored = null!;
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(next), ref restored);

        Assert.Equal(48_213, restored.Id);
        Assert.Null(restored.Bio);
        Assert.Equal(previous.Roles, restored.Roles);
    }

    /// <summary>Reads the previous idle save bytes as the next save and back.</summary>
    [Fact]
    public void IdleSaveVersionToleranceRoundTrips()
    {
        var previous = new BenchIdleSaveV1
        {
            PlayerId = 4_100_200,
            SavedAt = new DateTime(2026, 4, 5),
            Gold = 1.25e18,
            Gems = 999,
            LifetimeEarnings = 9.87e21,
            Resources = Enumerable.Range(0, 50).ToDictionary(resource => resource, resource => resource * 1.5),
            Buildings = Enumerable.Range(0, 40).Select(index => new BenchBuilding
            {
                Id = (short)index,
                Level = index + 1,
                Multiplier = 1 + index,
                UpgradeEndsAt = index % 5 == 0 ? null : new DateTime(2026, 6, 7),
            }).ToList(),
            Upgrades = Enumerable.Range(0, 120).ToDictionary(index => 5_000 + index, index => (byte)(1 + index % 5)),
            Offline = new BenchOfflineState
            {
                LastSeen = new DateTime(2026, 4, 4),
                RatePerSecond = 3.3e9,
                CapHours = 8,
                Pending = 1e12,
            },
            Stats = Enumerable.Range(0, 100).ToDictionary(stat => stat, stat => (long)stat * 1_000_000),
            Achievements = Enumerable.Range(0, 60).Select(index => 9_000 + index * 3).ToList(),
        };

        BenchIdleSaveV2 next = null!;
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(previous), ref next);

        Assert.Equal(4_100_200, next.PlayerId);
        Assert.Equal(40, next.Buildings.Count);
        Assert.Equal(40, next.Buildings[39].Level);
        Assert.Equal(73.5, next.Resources[49]);
        Assert.Equal(99_000_000, next.Stats[99]);
        Assert.Equal(9_000 + 59 * 3, next.Achievements[59]);
        Assert.Equal(1e12, next.Offline.Pending);
        Assert.Equal(0, next.PrestigeCount);
        Assert.Equal(0, next.PrestigeMultiplier);

        next.PrestigeCount = 3;
        next.PrestigeMultiplier = 2.75;
        BenchIdleSaveV1 restored = null!;
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(next), ref restored);

        Assert.Equal(4_100_200, restored.PlayerId);
        Assert.Equal(40, restored.Buildings[39].Level);
        Assert.Equal(8, restored.Offline.CapHours);
    }
}

#endif
