// Adapted from the Nino benchmark suite. See LICENSE.Nino.txt.
using LuminPack.Attribute;
using System;
using System.Collections.Generic;
using System.Linq;
using Nino.Core;

#nullable disable
namespace LuminPack.NinoBenchmark.Workloads
{

    // ---------------------------------------------------------------- State sync: a server's snapshot of one tick, 64
    // entities whose components are an interface (full entities carry every component, the others only what changed).
    // The client deserializes it into the snapshot it already holds.

    [NinoType, LuminPackable]
    public partial interface IComponent { }

    [NinoType, LuminPackable]
    public sealed partial class TransformComponent : IComponent
    {
        public Vec3 Position;
        public Quat Rotation;
    }

    [NinoType, LuminPackable]
    public sealed partial class VelocityComponent : IComponent
    {
        public Vec3 Linear;
        public float Angular;
    }

    [NinoType, LuminPackable]
    public sealed partial class HealthComponent : IComponent
    {
        public int Hp;
        public int MaxHp;
        public int Shield;
    }

    [NinoType, LuminPackable]
    public partial struct Buff
    {
        public short Id;
        public byte Stacks;
        public float Remaining;
    }

    [NinoType, LuminPackable]
    public sealed partial class BuffsComponent : IComponent
    {
        public List<Buff> Buffs;
    }

    [NinoType, LuminPackable]
    public sealed partial class AnimationComponent : IComponent
    {
        public short Clip;
        public float Time;
        public byte Layer;
    }

    [NinoType, LuminPackable]
    public sealed partial class NetEntity
    {
        public int Id;
        public short Archetype;
        public bool Full;
        public uint ChangedMask;
        public List<IComponent> Components;
    }

    [NinoType, LuminPackable]
    public sealed partial class StateSnapshot
    {
        public long Tick;
        public long AckedInputTick;
        public List<NetEntity> Entities;

        /// <summary>Creates one tick of entity and component state.</summary>
        /// <param name="r">The seeded random number generator.</param>
        /// <returns>The generated fixture.</returns>
        public static StateSnapshot Create(Random r) => new()
        {
            Tick = 1_843_200, AckedInputTick = 1_843_197,
            Entities = Enumerable.Range(0, 64).Select(i =>
            {
                bool full = i % 4 == 0;
                var all = new List<IComponent>
                {
                    new TransformComponent { Position = Data.V(r, 500), Rotation = Data.Q(r) },
                    new VelocityComponent { Linear = Data.V(r, 8), Angular = r.NextSingle() },
                    new HealthComponent { Hp = r.Next(1, 1_000), MaxHp = 1_000, Shield = r.Next(200) },
                    new BuffsComponent { Buffs = Enumerable.Range(0, r.Next(1, 5))
                        .Select(_ => new Buff { Id = (short)r.Next(300), Stacks = (byte)r.Next(1, 5), Remaining = r.NextSingle() * 10 }).ToList() },
                    new AnimationComponent { Clip = (short)r.Next(40), Time = r.NextSingle(), Layer = (byte)r.Next(3) },
                };
                var components = full ? all : all.Where((_, c) => c < 2 || r.Next(4) == 0).Take(r.Next(1, 4)).ToList();
                return new NetEntity
                {
                    Id = 10_000 + i, Archetype = (short)r.Next(12), Full = full,
                    ChangedMask = full ? 0b11111u : (uint)r.Next(1, 32), Components = components,
                };
            }).ToList(),
        };
    }

    // ---------------------------------------------------------------- Lockstep: what a frame-sync server broadcasts,
    // 10 frames of 8 players' input commands (an interface), the last frames repeated for loss.

    [NinoType, LuminPackable]
    public partial interface ICommand { }

    [NinoType, LuminPackable]
    public sealed partial class MoveCommand : ICommand
    {
        public short DirX;
        public short DirY;
    }

    [NinoType, LuminPackable]
    public sealed partial class CastCommand : ICommand
    {
        public short SkillId;
        public int TargetId;
        public short X;
        public short Y;
    }

    [NinoType, LuminPackable]
    public sealed partial class UseItemCommand : ICommand
    {
        public int ItemId;
    }

    [NinoType, LuminPackable]
    public sealed partial class StopCommand : ICommand
    {
        public byte Reason;
    }

    [NinoType, LuminPackable]
    public sealed partial class PlayerInput
    {
        public byte Slot;
        public List<ICommand> Commands;
    }

    [NinoType, LuminPackable]
    public sealed partial class LockstepFrame
    {
        public int Frame;
        public List<PlayerInput> Inputs;
    }

    [NinoType, LuminPackable]
    public sealed partial class LockstepFrames
    {
        public int FirstFrame;
        public uint Checksum;
        public List<LockstepFrame> Frames;

        /// <summary>Creates the command frames sent to lockstep players.</summary>
        /// <param name="r">The seeded random number generator.</param>
        /// <returns>The generated fixture.</returns>
        public static LockstepFrames Create(Random r) => new()
        {
            FirstFrame = 36_120, Checksum = 0x9e3779b9,
            Frames = Enumerable.Range(0, 10).Select(f => new LockstepFrame
            {
                Frame = 36_120 + f,
                Inputs = Enumerable.Range(0, 8).Select(p => new PlayerInput
                {
                    Slot = (byte)p,
                    Commands = Enumerable.Range(0, r.Next(1, 3)).Select(_ => (ICommand)(r.Next(10) switch
                    {
                        < 6 => new MoveCommand { DirX = (short)r.Next(-1000, 1000), DirY = (short)r.Next(-1000, 1000) },
                        < 8 => new CastCommand { SkillId = (short)r.Next(1, 40), TargetId = r.Next(8), X = (short)r.Next(4096), Y = (short)r.Next(4096) },
                        8 => new UseItemCommand { ItemId = r.Next(100, 200) },
                        _ => new StopCommand { Reason = 1 },
                    })).ToList(),
                }).ToList(),
            }).ToList(),
        };
    }

    // ---------------------------------------------------------------- MMO: one AOI update to one client, about 200
    // entity events through an abstract base, and the chat lines of that tick.

    [NinoType, LuminPackable]
    public abstract partial class AoiEvent
    {
        public int EntityId;
    }

    [NinoType, LuminPackable]
    public sealed partial class EntityEnter : AoiEvent
    {
        public short Type;
        public string Name;
        public Vec3 Position;
        public int Level;
        public int? GuildId;
        public List<int> Equipment;
    }

    [NinoType, LuminPackable]
    public sealed partial class EntityLeave : AoiEvent
    {
        public byte Reason;
    }

    [NinoType, LuminPackable]
    public sealed partial class EntityMove : AoiEvent
    {
        public Vec3 Position;
        public Vec3 Direction;
        public float Speed;
    }

    [NinoType, LuminPackable]
    public partial struct DamageHit
    {
        public int TargetId;
        public int Amount;
        public bool Critical;
    }

    [NinoType, LuminPackable]
    public sealed partial class SkillCast : AoiEvent
    {
        public short SkillId;
        public Vec3 Target;
        public List<DamageHit> Hits;
    }

    [NinoType, LuminPackable]
    public sealed partial class BuffChanged : AoiEvent
    {
        public short BuffId;
        public byte Stacks;
        public float Duration;
    }

    [NinoType, LuminPackable]
    public sealed partial class ChatMessage
    {
        public byte Channel;
        public long SenderId;
        public string SenderName;
        public string Text;
        public DateTime SentAt;
    }

    [NinoType, LuminPackable]
    public sealed partial class AoiUpdate
    {
        public long ServerTick;
        public int SceneId;
        public List<AoiEvent> Events;
        public List<ChatMessage> Chat;

        private static readonly string[] Lines = {"lfg dungeon, need healer", "wts epic sword 500g", "gg", "anyone for raid tonight?",
            "where is the blacksmith", "guild recruiting, pm me"};

        /// <summary>Creates the area-of-interest update for one player.</summary>
        /// <param name="r">The seeded random number generator.</param>
        /// <returns>The generated fixture.</returns>
        public static AoiUpdate Create(Random r) => new()
        {
            ServerTick = 9_200_311, SceneId = 1_002,
            Events = Enumerable.Range(0, 200).Select(i => (AoiEvent)(r.Next(10) switch
            {
                0 => new EntityEnter { EntityId = 50_000 + i, Type = (short)r.Next(30), Name = Data.Name(r), Position = Data.V(r, 300),
                    Level = r.Next(1, 61), GuildId = r.Next(3) == 0 ? null : r.Next(1, 900),
                    Equipment = Enumerable.Range(0, 6).Select(_ => r.Next(10_000, 20_000)).ToList() },
                1 => new EntityLeave { EntityId = 50_000 + i, Reason = (byte)r.Next(3) },
                < 7 => new EntityMove { EntityId = 50_000 + i, Position = Data.V(r, 300), Direction = Data.V(r, 1), Speed = r.NextSingle() * 7 },
                < 9 => new SkillCast { EntityId = 50_000 + i, SkillId = (short)r.Next(1, 200), Target = Data.V(r, 300),
                    Hits = Enumerable.Range(0, r.Next(1, 6)).Select(_ => new DamageHit
                        { TargetId = r.Next(50_000, 50_200), Amount = r.Next(1, 5_000), Critical = r.Next(5) == 0 }).ToList() },
                _ => new BuffChanged { EntityId = 50_000 + i, BuffId = (short)r.Next(1, 300), Stacks = (byte)r.Next(1, 6), Duration = r.NextSingle() * 30 },
            })).ToList(),
            Chat = Enumerable.Range(0, 5).Select(_ => new ChatMessage
            {
                Channel = (byte)r.Next(4), SenderId = r.Next(1, 2_000_000), SenderName = Data.Name(r), Text = Lines[r.Next(Lines.Length)],
                SentAt = Data.Epoch.AddSeconds(r.Next(60)),
            }).ToList(),
        };
    }

    // ---------------------------------------------------------------- SLG: one region of the world map, 64 x 64 tiles,
    // its cities, the armies on it and their marches; cities and armies refer to their alliance, one object shared by
    // many. Both serializers track the shared references.

    [NinoType, LuminPackable]
    public partial struct Tile
    {
        public byte Terrain;
        public byte Level;
        public int OwnerId;
    }

    [NinoType(trackReferences: true)]
    [LuminPackable(GeneratorType.VersionTolerant), LuminPackOption(TrackReferences = true)]
    public sealed partial class Alliance
    {
        [LuminPackOrder(0)] public int Id;
        [LuminPackOrder(1)] public string Name;
        [LuminPackOrder(2)] public string Tag;
        [LuminPackOrder(3)] public int Members;
        [LuminPackOrder(4)] public long Power;
    }

    [NinoType, LuminPackable]
    public partial struct MapPoint
    {
        public short X;
        public short Y;
    }

    public enum MarchKind { Attack, Gather, Reinforce, Scout }

    [NinoType, LuminPackable]
    public sealed partial class March
    {
        public MarchKind Kind;
        public DateTime Departed;
        public DateTime Arrives;
        public List<MapPoint> Path;
    }

    [NinoType, LuminPackable]
    public sealed partial class City
    {
        public int Id;
        public string Name;
        public int OwnerId;
        public MapPoint Position;
        public byte Level;
        public Dictionary<int, int> Buildings;
        public Alliance Alliance;
    }

    [NinoType, LuminPackable]
    public sealed partial class Army
    {
        public long Id;
        public int OwnerId;
        public Alliance Alliance;
        public Dictionary<int, int> Troops;
        public March March;
    }

    [NinoType, LuminPackable]
    public sealed partial class WorldRegion
    {
        public short RegionX;
        public short RegionY;
        public Tile[] Tiles;
        public List<Alliance> Alliances;
        public List<City> Cities;
        public List<Army> Armies;

        /// <summary>Creates a map region whose cities and armies share alliances.</summary>
        /// <param name="r">The seeded random number generator.</param>
        /// <returns>The generated fixture.</returns>
        public static WorldRegion Create(Random r)
        {
            var alliances = Enumerable.Range(0, 6).Select(i => new Alliance
                { Id = 700 + i, Name = "Alliance " + (char)('A' + i), Tag = "AL" + i, Members = r.Next(20, 100), Power = r.NextInt64(1_000_000, 90_000_000) }).ToList();
            MapPoint P() => new() { X = (short)r.Next(64), Y = (short)r.Next(64) };
            return new WorldRegion
            {
                RegionX = 12, RegionY = 7, Alliances = alliances,
                Tiles = Enumerable.Range(0, 64 * 64).Select(_ => new Tile
                    { Terrain = (byte)r.Next(6), Level = (byte)r.Next(1, 9), OwnerId = r.Next(4) == 0 ? r.Next(1, 500_000) : 0 }).ToArray(),
                Cities = Enumerable.Range(0, 40).Select(i => new City
                {
                    Id = 30_000 + i, Name = "City " + i, OwnerId = r.Next(1, 500_000), Position = P(), Level = (byte)r.Next(1, 26),
                    Buildings = Enumerable.Range(0, 8).ToDictionary(b => 100 + b, _ => r.Next(1, 26)), Alliance = alliances[r.Next(6)],
                }).ToList(),
                Armies = Enumerable.Range(0, 120).Select(i => new Army
                {
                    Id = 8_000_000 + i, OwnerId = r.Next(1, 500_000), Alliance = alliances[r.Next(6)],
                    Troops = Enumerable.Range(0, 4).ToDictionary(t => t + 1, _ => r.Next(100, 50_000)),
                    March = r.Next(3) == 0 ? null : new March
                    {
                        Kind = (MarchKind)r.Next(4), Departed = Data.Epoch.AddSeconds(r.Next(3_600)),
                        Arrives = Data.Epoch.AddSeconds(r.Next(3_600, 9_000)),
                        Path = Enumerable.Range(0, r.Next(8, 30)).Select(_ => P()).ToList(),
                    },
                }).ToList(),
            };
        }
    }

    // ---------------------------------------------------------------- Card game: the replay of one battle, 300 actions in
    // two levels of abstract classes (spell effects under the action base), referring to the battle's card definitions,
    // one object per card shared by decks, hands and actions.

    public enum CardType { Minion, Spell, Weapon }

    [NinoType(trackReferences: true)]
    [LuminPackable(GeneratorType.VersionTolerant), LuminPackOption(TrackReferences = true)]
    public sealed partial class CardDef
    {
        [LuminPackOrder(0)] public int Id;
        [LuminPackOrder(1)] public string Name;
        [LuminPackOrder(2)] public byte Cost;
        [LuminPackOrder(3)] public CardType Type;
        [LuminPackOrder(4)] public short Attack;
        [LuminPackOrder(5)] public short Health;
        [LuminPackOrder(6)] public string Text;
    }

    [NinoType, LuminPackable]
    public abstract partial class CardAction
    {
        public short Turn;
        public byte Actor;
    }

    [NinoType, LuminPackable]
    public sealed partial class PlayCard : CardAction
    {
        public CardDef Card;
        public byte Slot;
    }

    [NinoType, LuminPackable]
    public sealed partial class AttackAction : CardAction
    {
        public int AttackerUid;
        public int TargetUid;
        public short Damage;
    }

    /// <summary>The second level: what a spell card does, with the card that did it.</summary>
    [NinoType, LuminPackable]
    public abstract partial class SpellEffect : CardAction
    {
        public CardDef Source;
    }

    [NinoType, LuminPackable]
    public sealed partial class DealDamage : SpellEffect
    {
        public List<int> Targets;
        public short Amount;
    }

    [NinoType, LuminPackable]
    public sealed partial class Heal : SpellEffect
    {
        public int Target;
        public short Amount;
    }

    [NinoType, LuminPackable]
    public sealed partial class Summon : SpellEffect
    {
        public CardDef Minion;
        public byte Slot;
    }

    [NinoType, LuminPackable]
    public sealed partial class DrawCard : CardAction
    {
        public CardDef Card;
    }

    [NinoType, LuminPackable]
    public sealed partial class EndTurn : CardAction
    {
        public int Elapsed;
    }

    [NinoType, LuminPackable]
    public sealed partial class BattlePlayer
    {
        public long PlayerId;
        public string Name;
        public int Hero;
        public List<CardDef> Deck;
        public List<CardDef> Hand;
    }

    [NinoType, LuminPackable]
    public sealed partial class CardBattleLog
    {
        public long BattleId;
        public List<CardDef> Cards;
        public List<BattlePlayer> Players;
        public List<CardAction> Actions;

        /// <summary>Creates a battle replay with shared card definitions.</summary>
        /// <param name="r">The seeded random number generator.</param>
        /// <returns>The generated fixture.</returns>
        public static CardBattleLog Create(Random r)
        {
            var cards = Enumerable.Range(0, 40).Select(i => new CardDef
            {
                Id = 1_000 + i, Name = "Card " + i, Cost = (byte)r.Next(0, 11), Type = (CardType)(i % 3),
                Attack = (short)r.Next(0, 12), Health = (short)r.Next(1, 12),
                Text = i % 4 == 0 ? "Battlecry: deal 2 damage." : "Taunt. Deathrattle: summon a 1/1.",
            }).ToList();
            CardDef C() => cards[r.Next(cards.Count)];
            return new CardBattleLog
            {
                BattleId = 66_000_123, Cards = cards,
                Players = Enumerable.Range(0, 2).Select(p => new BattlePlayer
                {
                    PlayerId = 2_000_000 + p, Name = Data.Name(r), Hero = r.Next(1, 10),
                    Deck = Enumerable.Range(0, 30).Select(_ => C()).ToList(), Hand = Enumerable.Range(0, 5).Select(_ => C()).ToList(),
                }).ToList(),
                Actions = Enumerable.Range(0, 300).Select(i =>
                {
                    CardAction a = r.Next(10) switch
                    {
                        < 3 => new PlayCard { Card = C(), Slot = (byte)r.Next(7) },
                        < 5 => new AttackAction { AttackerUid = r.Next(1, 60), TargetUid = r.Next(1, 60), Damage = (short)r.Next(1, 10) },
                        5 => new DealDamage { Source = C(), Targets = Enumerable.Range(0, r.Next(1, 4)).Select(_ => r.Next(1, 60)).ToList(), Amount = (short)r.Next(1, 8) },
                        6 => new Heal { Source = C(), Target = r.Next(1, 60), Amount = (short)r.Next(1, 8) },
                        7 => new Summon { Source = C(), Minion = C(), Slot = (byte)r.Next(7) },
                        8 => new DrawCard { Card = C() },
                        _ => new EndTurn { Elapsed = r.Next(5_000, 75_000) },
                    };
                    a.Turn = (short)(i / 12 + 1);
                    a.Actor = (byte)(i / 12 % 2);
                    return a;
                }).ToList(),
            };
        }
    }
}
