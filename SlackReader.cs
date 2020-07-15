using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ProcessSlackExport
{
    class SlackReader
    {
        Dictionary<string, User> Users;
        string Folder;
        ZipArchive Archive;

        public Channel[] Channels { get; private set; }

        public SlackReader(string folder)
        {
            Folder = folder;
            ReadUsers();
            ReadChannels();
        }

        public SlackReader(ZipArchive archive)
        {
            Archive = archive;
            ReadUsers();
            ReadChannels();
        }

        string GetFileContents(string path)
        {
            if (Archive != null)
            {
                using var fs = Archive.GetEntry(path).Open();
                using var sr = new StreamReader(fs);
                return sr.ReadToEnd();
            }
            else
                return File.ReadAllText($"{Folder}/{path}");
        }

        void ReadChannels()
            => Channels = JsonConvert.DeserializeObject<Channel[]>(GetFileContents("channels.json"));

        void ReadUsers()
        {
            Users = JArray.Parse(GetFileContents("users.json"))
                .Select(r => new User
                {
                    ID = (string)r["id"],
                    DisplayName = r["profile"]["display_name"]?.ToString().Coalesce((string)r["profile"]["real_name"])
                })
                .ToDictionary(r => r.ID);
        }

        IEnumerable<string> GetFiles(string folderName)
        {
            if (Archive != null)
                return Archive.Entries.Where(e => e.FullName.Split('/').First() == folderName).Select(e => GetFileContents(e.FullName));
            else
                return Directory.GetFiles($"{Folder}/{folderName}").Select(f => File.ReadAllText(f));
        }

        public IEnumerable<Message> Read(Channel channel)
        {
            return GetFiles(channel.Name)
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .SelectMany(f => ReadFile(f))
                .Where(r => r.subtype == null) // filter out channel joins
                .GroupBy(r => r.thread_ts ?? r.ts)
                .Select(g => Convert(g.Single(r => r.ts == g.Key), g.Where(r => r.ts != g.Key).Select(r => Convert(r))))
                .AsEnumerable();
        }

        Message Convert(RawMessage msg, IEnumerable<Message> replies = null)
            => new Message
            {
                AuthorName = Users.GetValueOrDefault(msg.user)?.DisplayName ?? msg.user,
                Date = msg.Date,
                Body = msg.text,
                Replies = replies?.ToArray(),
                Reader = this
            };

        IEnumerable<RawMessage> ReadFile(string content)
            => JsonConvert.DeserializeObject<RawMessage[]>(content);

        public string FormatUsers(string body)
        {
            var builder = new StringBuilder(body);
            Users.Values.ForEach(u => builder.Replace($"<@{u.ID}>", u.DisplayName));
            return builder.ToString();
        }


        class RawMessage
        {
            public string text { get; set; }
            public double ts { get; set; }
            public double? thread_ts { get; set; }
            public string user { get; set; }
            public string subtype { get; set; }

            public DateTime Date => DateTimeOffset.FromUnixTimeSeconds((long)ts).DateTime.ToLocalTime();

        }

        class User
        {
            public string DisplayName { get; set; }
            public string ID { get; set; }

            public override string ToString() => DisplayName;
        }
    }

    public class Channel
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("is_archived")]
        public bool IsArchived { get; set; }
        [JsonProperty("id")]
        public string ID { get; set; }

        public override string ToString() => Name;
    }

    public class Message
    {
        public string AuthorName { get; set; }
        public DateTime Date { get; set; }
        public string Body { get; set; }

        public Message[] Replies { get; set; }

        public override string ToString() => $"{Date:d MMM}, {AuthorName}: {Body}";

        internal SlackReader Reader;

        public string FormatAsHtml()
        {
            var res = $"<div style='font-size: smaller; margin-bottom: 1px'><span style='font-weight: bold; padding-right: 15px'>{AuthorName}</span> {Date.ToString("d MMM HH:mm")}</div>{Reader.FormatUsers(Body).Replace("\n", "\n<br/>")}";
            if (Replies?.Any() == true)
                res += $"<div style='margin-left: 15px'>{string.Join("", Replies.Select(r => $"<div style='margin-top: 5px'>{r.FormatAsHtml()}</div>"))}</div>";
            return res;
        }
    }
}