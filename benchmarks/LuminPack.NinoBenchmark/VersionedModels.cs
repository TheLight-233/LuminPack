// Adapted from the Nino benchmark suite. See LICENSE.Nino.txt.
using LuminPack.Attribute;
using LuminPack.Option;
using System;
using System.Collections.Generic;
using System.Linq;
using Nino.Core;

#nullable disable
namespace LuminPack.NinoBenchmark.Workloads
{

    // ---------------------------------------------------------------- A cached profile written by the previous release
    // (ProfileV1) and read by this one (ProfileV2 removes AvatarUrl and adds Pronouns and Badges). The workload
    // uses Nino's keyed format and LuminPack's version-tolerant format. The two types are sealed so the fixture
    // measures compatible field layouts without encoding different polymorphic type IDs.

    [NinoType(format: NinoFormat.Keyed)]
    [LuminPackable(GeneratorType.VersionTolerant)]
    public sealed partial class ProfileV1
    {
        [LuminPackOrder(0)] public long Id;
        [LuminPackOrder(1)] public string UserName;
        [LuminPackOrder(2)] public string Email;
        [LuminPackOrder(3)] public string DisplayName;
        [LuminPackOrder(4)] public string AvatarUrl;
        [LuminPackOrder(5)] public string Bio;
        [LuminPackOrder(6)] public DateTime CreatedAt;
        [LuminPackOrder(7)] public DateTime? LastLoginAt;
        [LuminPackOrder(8)] public bool EmailVerified;
        [LuminPackOrder(9)] public int? BirthYear;
        [LuminPackOrder(10)] public string Country;
        [LuminPackOrder(11)] public string TimeZone;
        [LuminPackOrder(12)] public int Followers;
        [LuminPackOrder(13)] public List<string> Roles;
        [LuminPackOrder(14)] public Dictionary<string, string> Preferences;

        /// <summary>Creates a profile in the previous schema.</summary>
        /// <param name="r">The seeded random number generator.</param>
        /// <returns>The generated fixture.</returns>
        public static ProfileV1 Create(Random r) => new()
        {
            Id = 48_213, UserName = "jdoe48213", Email = "jane.doe48213@example.com", DisplayName = "Jane Doe",
            AvatarUrl = "https://cdn.example.com/avatars/48213/256.png", Bio = null,
            CreatedAt = Data.Epoch.AddDays(-r.Next(2_000)), LastLoginAt = Data.Epoch.AddSeconds(r.Next()),
            EmailVerified = true, BirthYear = null, Country = "US", TimeZone = "America/Los_Angeles",
            Followers = r.Next(10_000), Roles = new List<string> { "member", "beta-tester" },
            Preferences = new Dictionary<string, string>
            {
                ["theme"] = "dark", ["language"] = "en", ["newsletter"] = "weekly", ["notifications.email"] = "true",
                ["notifications.push"] = "false", ["currency"] = "USD", ["units"] = "metric", ["startPage"] = "/dashboard",
                ["density"] = "compact", ["privacy.profile"] = "friends",
            },
        };
    }

    [NinoType(format: NinoFormat.Keyed)]
    [LuminPackable(GeneratorType.VersionTolerant)]
    public sealed partial class ProfileV2
    {
        [LuminPackOrder(0)] public long Id;
        [LuminPackOrder(1)] public string UserName;
        [LuminPackOrder(2)] public string Email;
        [LuminPackOrder(3)] public string DisplayName;
        // 4: AvatarUrl, removed.
        [LuminPackOrder(5)] public string Bio;
        [LuminPackOrder(6)] public DateTime CreatedAt;
        [LuminPackOrder(7)] public DateTime? LastLoginAt;
        [LuminPackOrder(8)] public bool EmailVerified;
        [LuminPackOrder(9)] public int? BirthYear;
        [LuminPackOrder(10)] public string Country;
        [LuminPackOrder(11)] public string TimeZone;
        [LuminPackOrder(12)] public int Followers;
        [LuminPackOrder(13)] public List<string> Roles;
        [LuminPackOrder(14)] public Dictionary<string, string> Preferences;
        [LuminPackOrder(15)] public string Pronouns;
        [LuminPackOrder(16)] public List<string> Badges;

        /// <summary>Creates the current profile from a previous-schema profile.</summary>
        /// <param name="v">The previous-schema value.</param>
        /// <returns>The current profile with its new fields populated.</returns>
        public static ProfileV2 From(ProfileV1 v) => new()
        {
            Id = v.Id, UserName = v.UserName, Email = v.Email, DisplayName = v.DisplayName, Bio = v.Bio,
            CreatedAt = v.CreatedAt, LastLoginAt = v.LastLoginAt, EmailVerified = v.EmailVerified, BirthYear = v.BirthYear,
            Country = v.Country, TimeZone = v.TimeZone, Followers = v.Followers, Roles = v.Roles, Preferences = v.Preferences,
            Pronouns = "she/her", Badges = new List<string> { "early-adopter", "top-reviewer" },
        };
    }

    // ---------------------------------------------------------------- Idle game: a save written by the previous
    // release (IdleSaveV1) and read by this one (IdleSaveV2 appends prestige members) into the save the game already holds.

    [NinoType, LuminPackable]
    public sealed partial class Building
    {
        public short Id;
        public int Level;
        public double Multiplier;
        public DateTime? UpgradeEndsAt;
    }

    [NinoType, LuminPackable]
    public sealed partial class OfflineState
    {
        public DateTime LastSeen;
        public double RatePerSecond;
        public double CapHours;
        public double Pending;
    }

    [NinoType(format: NinoFormat.VersionTolerant)]
    [LuminPackable(GeneratorType.VersionTolerant)]
    public sealed partial class IdleSaveV1
    {
        [LuminPackOrder(0)] public long PlayerId;
        [LuminPackOrder(1)] public DateTime SavedAt;
        [LuminPackOrder(2)] public double Gold;
        [LuminPackOrder(3)] public double Gems;
        [LuminPackOrder(4)] public double LifetimeEarnings;
        [LuminPackOrder(5)] public Dictionary<int, double> Resources;
        [LuminPackOrder(6)] public List<Building> Buildings;
        [LuminPackOrder(7)] public Dictionary<int, byte> Upgrades;
        [LuminPackOrder(8)] public OfflineState Offline;
        [LuminPackOrder(9)] public Dictionary<int, long> Stats;
        [LuminPackOrder(10)] public List<int> Achievements;

        /// <summary>Creates an idle-game save in the previous schema.</summary>
        /// <param name="r">The seeded random number generator.</param>
        /// <returns>The generated fixture.</returns>
        public static IdleSaveV1 Create(Random r) => new()
        {
            PlayerId = 4_100_200, SavedAt = Data.Epoch.AddHours(r.Next(1_000)), Gold = 1.234e18 * r.NextDouble(),
            Gems = r.Next(5_000), LifetimeEarnings = 9.87e21 * r.NextDouble(),
            Resources = Enumerable.Range(0, 50).ToDictionary(i => i, _ => Math.Pow(10, r.Next(3, 30)) * r.NextDouble()),
            Buildings = Enumerable.Range(0, 40).Select(i => new Building
            {
                Id = (short)i, Level = r.Next(1, 2_000), Multiplier = 1 + r.NextDouble() * 99,
                UpgradeEndsAt = r.Next(5) == 0 ? Data.Epoch.AddMinutes(r.Next(600)) : null,
            }).ToList(),
            Upgrades = Enumerable.Range(0, 120).ToDictionary(i => 5_000 + i, _ => (byte)r.Next(1, 6)),
            Offline = new OfflineState
                { LastSeen = Data.Epoch.AddHours(r.Next(1_000)), RatePerSecond = 3.3e9 * r.NextDouble(), CapHours = 8, Pending = 1e12 * r.NextDouble() },
            Stats = Enumerable.Range(0, 100).ToDictionary(i => i, _ => r.NextInt64(0, long.MaxValue / 1024)),
            Achievements = Enumerable.Range(0, 60).Select(i => 9_000 + i * 3).ToList(),
        };
    }

    [NinoType(format: NinoFormat.VersionTolerant)]
    [LuminPackable(GeneratorType.VersionTolerant)]
    public sealed partial class IdleSaveV2
    {
        [LuminPackOrder(0)] public long PlayerId;
        [LuminPackOrder(1)] public DateTime SavedAt;
        [LuminPackOrder(2)] public double Gold;
        [LuminPackOrder(3)] public double Gems;
        [LuminPackOrder(4)] public double LifetimeEarnings;
        [LuminPackOrder(5)] public Dictionary<int, double> Resources;
        [LuminPackOrder(6)] public List<Building> Buildings;
        [LuminPackOrder(7)] public Dictionary<int, byte> Upgrades;
        [LuminPackOrder(8)] public OfflineState Offline;
        [LuminPackOrder(9)] public Dictionary<int, long> Stats;
        [LuminPackOrder(10)] public List<int> Achievements;
        [LuminPackOrder(11)] public int PrestigeCount;
        [LuminPackOrder(12)] public double PrestigeMultiplier;

        /// <summary>Creates the current idle-game save from a previous-schema save.</summary>
        /// <param name="v">The previous-schema value.</param>
        /// <returns>The current save with its new fields populated.</returns>
        public static IdleSaveV2 From(IdleSaveV1 v) => new()
        {
            PlayerId = v.PlayerId, SavedAt = v.SavedAt, Gold = v.Gold, Gems = v.Gems, LifetimeEarnings = v.LifetimeEarnings,
            Resources = v.Resources, Buildings = v.Buildings, Upgrades = v.Upgrades, Offline = v.Offline, Stats = v.Stats,
            Achievements = v.Achievements, PrestigeCount = 3, PrestigeMultiplier = 2.75,
        };
    }
}
