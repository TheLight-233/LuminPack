// Adapted from the Nino benchmark suite. See LICENSE.Nino.txt.
using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.Attribute;
using Nino.Core;

#nullable disable
namespace LuminPack.NinoBenchmark.Workloads
{

    // Two payloads of the common serializer benchmarks, so these numbers can be set beside theirs.

    public enum MediaPlayer { Java, Flash }

    public enum ImageSize { Small, Large }

    /// <summary>jvm-serializers' Media (tpc/schema/media.proto), the payload every language's serializers are compared on.</summary>
    [NinoType, LuminPackable]
    public partial class Media
    {
        public string Uri;
        public string Title;
        public int Width;
        public int Height;
        public string Format;
        public long Duration;
        public long Size;
        public int Bitrate;
        public List<string> Persons;
        public MediaPlayer Player;
        public string Copyright;
    }

    [NinoType, LuminPackable]
    public partial class Image
    {
        public string Uri;
        public string Title;
        public int Width;
        public int Height;
        public ImageSize Size;
    }

    /// <summary>jvm-serializers' MediaContent with its standard values (StdMediaSerializer's test data).</summary>
    [NinoType, LuminPackable]
    public partial class MediaContent
    {
        public List<Image> Images;
        public Media Media;

        /// <summary>Creates the standard fixed media content fixture.</summary>
        /// <param name="_">The unused fixture seed.</param>
        /// <returns>The media content fixture.</returns>
        public static MediaContent Create(Random _) => new()
        {
            Media = new Media
            {
                Uri = "http://javaone.com/keynote.mpg", Title = "Javaone Keynote", Width = 640, Height = 480,
                Format = "video/mpg4", Duration = 18_000_000, Size = 58_982_400, Bitrate = 262_144,
                Persons = new List<string> { "Bill Gates", "Steve Jobs" }, Player = MediaPlayer.Java, Copyright = null,
            },
            Images = new List<Image>
            {
                new Image { Uri = "http://javaone.com/keynote_large.jpg", Title = "Javaone Keynote", Width = 1024, Height = 768, Size = ImageSize.Large },
                new Image { Uri = "http://javaone.com/keynote_small.jpg", Title = "Javaone Keynote", Width = 320, Height = 240, Size = ImageSize.Small },
            },
        };
    }

    public enum UserType { Unregistered, Registered, Moderator, DoesNotExist }

    [NinoType, LuminPackable]
    public partial class BadgeCount
    {
        public int Gold;
        public int Silver;
        public int Bronze;
    }

    /// <summary>The Stack Exchange API's shallow_user, as Jil's benchmark models (and MessagePack-CSharp's) carry it.</summary>
    [NinoType, LuminPackable]
    public partial class ShallowUser
    {
        public int? UserId;
        public string DisplayName;
        public int? Reputation;
        public UserType UserType;
        public string ProfileImage;
        public string Link;
        public int? AcceptRate;
        public BadgeCount BadgeCounts;

        /// <summary>Creates a user for the Q&amp;A fixture.</summary>
        /// <param name="r">The seeded random number generator.</param>
        /// <returns>The generated fixture.</returns>
        public static ShallowUser Create(Random r)
        {
            int id = r.Next(1, 9_000_000);
            return new ShallowUser
            {
                UserId = id, DisplayName = Data.Name(r), Reputation = r.Next(1, 400_000), UserType = UserType.Registered,
                ProfileImage = $"https://i.sstatic.net/{id:x8}.jpg?s=256", Link = $"https://stackoverflow.com/users/{id}",
                AcceptRate = r.Next(3) == 0 ? null : r.Next(40, 100),
                BadgeCounts = new BadgeCount { Gold = r.Next(50), Silver = r.Next(500), Bronze = r.Next(900) },
            };
        }
    }

    [NinoType, LuminPackable]
    public partial class Comment
    {
        public int CommentId;
        public int PostId;
        public DateTime CreationDate;
        public int Score;
        public bool Edited;
        public string Body;
        public ShallowUser Owner;
        public ShallowUser ReplyToUser;
        public string Link;
    }

    [NinoType, LuminPackable]
    public partial class Answer
    {
        public int AnswerId;
        public int QuestionId;
        public DateTime CreationDate;
        public DateTime LastActivityDate;
        public DateTime? LastEditDate;
        public int Score;
        public bool IsAccepted;
        public string Body;
        public ShallowUser Owner;
        public List<Comment> Comments;
        public int UpVoteCount;
        public int DownVoteCount;
        public string Link;
    }

    /// <summary>The Stack Exchange API's question, the fields Jil's benchmark model has that a page of questions fills.</summary>
    [NinoType, LuminPackable]
    public partial class Question
    {
        public int QuestionId;
        public DateTime CreationDate;
        public DateTime LastActivityDate;
        public DateTime? LastEditDate;
        public DateTime? LockedDate;
        public DateTime? ClosedDate;
        public string ClosedReason;
        public int AnswerCount;
        public int? AcceptedAnswerId;
        public int? BountyAmount;
        public DateTime? BountyClosesDate;
        public string Title;
        public string Body;
        public List<string> Tags;
        public int UpVoteCount;
        public int DownVoteCount;
        public int FavoriteCount;
        public int ViewCount;
        public int Score;
        public bool IsAnswered;
        public ShallowUser Owner;
        public List<Answer> Answers;
        public List<Comment> Comments;
        public string Link;
    }

    /// <summary>
    /// A page of 20 questions with their answers and comments in the Stack Exchange API's wrapper, the models Jil's
    /// benchmark suite brought to .NET and MessagePack-CSharp's and MemoryPack's benchmarks reuse.
    /// </summary>
    [NinoType, LuminPackable]
    public partial class StackOverflowPage
    {
        public List<Question> Items;
        public bool HasMore;
        public int QuotaMax;
        public int QuotaRemaining;
        public int Page;
        public int PageSize;
        public int Total;

        private static readonly string[] TagNames = {"c#", ".net", "serialization", "performance", "asp.net-core",
            "unity3d", "protobuf", "json", "messagepack", "generics", "linq", "async-await"};

        /// <summary>Creates comments belonging to a question or an answer.</summary>
        /// <param name="r">The seeded random number generator.</param>
        /// <param name="post">The parent post identifier.</param>
        /// <param name="count">The number of comments to generate.</param>
        /// <returns>The generated comments.</returns>
        private static List<Comment> Comments(Random r, int post, int count) => Enumerable.Range(0, count).Select(_ =>
        {
            int id = r.Next(100_000_000, 140_000_000);
            return new Comment
            {
                CommentId = id, PostId = post, CreationDate = Data.Epoch.AddMinutes(r.Next(200_000)), Score = r.Next(12),
                Edited = r.Next(5) == 0, Body = Data.Html(r, 1), Owner = ShallowUser.Create(r),
                ReplyToUser = r.Next(3) == 0 ? ShallowUser.Create(r) : null,
                Link = $"https://stackoverflow.com/questions/{post}#comment{id}_{post}",
            };
        }).ToList();

        /// <summary>Creates one page of questions and answers.</summary>
        /// <param name="r">The seeded random number generator.</param>
        /// <returns>The generated fixture.</returns>
        public static StackOverflowPage Create(Random r) => new()
        {
            HasMore = true, QuotaMax = 10_000, QuotaRemaining = 9_312, Page = 1, PageSize = 20, Total = 24_118_563,
            Items = Enumerable.Range(0, 20).Select(_ =>
            {
                int id = r.Next(70_000_000, 79_000_000);
                var created = Data.Epoch.AddMinutes(r.Next(500_000));
                var answers = Enumerable.Range(0, r.Next(1, 5)).Select(_ =>
                {
                    int answer = r.Next(70_000_000, 79_000_000);
                    return new Answer
                    {
                        AnswerId = answer, QuestionId = id, CreationDate = created.AddMinutes(r.Next(1, 600)),
                        LastActivityDate = created.AddMinutes(r.Next(600, 6_000)),
                        LastEditDate = r.Next(2) == 0 ? created.AddMinutes(r.Next(600, 6_000)) : null, Score = r.Next(-2, 90),
                        IsAccepted = r.Next(4) == 0, Body = Data.Html(r, r.Next(3, 9)), Owner = ShallowUser.Create(r),
                        Comments = Comments(r, answer, r.Next(1, 4)), UpVoteCount = r.Next(100), DownVoteCount = r.Next(4),
                        Link = $"https://stackoverflow.com/a/{answer}",
                    };
                }).ToList();
                bool closed = r.Next(10) == 0;
                return new Question
                {
                    QuestionId = id, CreationDate = created, LastActivityDate = created.AddMinutes(r.Next(10, 9_000)),
                    LastEditDate = r.Next(2) == 0 ? created.AddMinutes(r.Next(5, 600)) : null, LockedDate = null,
                    ClosedDate = closed ? created.AddDays(1) : null, ClosedReason = closed ? "Duplicate" : null,
                    AnswerCount = answers.Count, AcceptedAnswerId = answers.FirstOrDefault(a => a.IsAccepted)?.AnswerId,
                    BountyAmount = r.Next(8) == 0 ? 50 * r.Next(1, 10) : null, BountyClosesDate = null,
                    Title = "How do I serialize a " + TagNames[r.Next(TagNames.Length)] + " type without allocating?",
                    Body = Data.Html(r, r.Next(4, 12)),
                    Tags = Enumerable.Range(0, r.Next(1, 5)).Select(_ => TagNames[r.Next(TagNames.Length)]).Distinct().ToList(),
                    UpVoteCount = r.Next(200), DownVoteCount = r.Next(10), FavoriteCount = r.Next(40), ViewCount = r.Next(90_000),
                    Score = r.Next(-3, 190), IsAnswered = answers.Count > 0, Owner = ShallowUser.Create(r), Answers = answers,
                    Comments = Comments(r, id, r.Next(1, 4)), Link = $"https://stackoverflow.com/questions/{id}",
                };
            }).ToList(),
        };
    }
}
