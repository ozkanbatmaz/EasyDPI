using System;
using System.Collections.Generic;

namespace EasyDPI
{
    /// <summary>One address the tuner tests, and what its failure means.</summary>
    sealed class ProbeHost
    {
        /// <summary>Host name, tested over HTTPS on port 443.</summary>
        public readonly string Host;

        /// <summary>Service this address belongs to, used to group the report.</summary>
        public readonly string Group;

        /// <summary>
        /// True for addresses that are expected to work everywhere. These are not
        /// targets to unblock; they exist to catch settings that break ordinary
        /// browsing, which is a worse outcome than leaving a site blocked.
        /// </summary>
        public readonly bool IsControl;

        public ProbeHost(string host, string group, bool isControl)
        {
            Host = host;
            Group = group;
            IsControl = isControl;
        }
    }

    /// <summary>
    /// The addresses the tuner measures.
    ///
    /// Two things make this list worth more than a handful of domain names.
    ///
    /// First, it is per-endpoint rather than per-site. A provider does not block
    /// "roblox.com"; it blocks names, and a setting that lets the home page load
    /// while setup.roblox.com and clientsettingscdn.roblox.com stay dead produces
    /// exactly the half-broken experience people report — the installer warning
    /// about missing flag settings, profile pages that never fill in, server lists
    /// that stay empty. Testing the endpoints an application actually calls is the
    /// only way to see that.
    ///
    /// Second, every entry was verified before being added: each name was resolved
    /// and then fetched over HTTPS from a real connection, and anything that no
    /// longer exists was dropped rather than carried along as decoration. Roblox's
    /// own education-network article, for example, still lists api.roblox.com,
    /// which has no address records at all any more.
    ///
    /// Sources:
    ///   Roblox      - help.roblox.com "Troubleshooting Education Networks" allowlist,
    ///                 plus the endpoints the client and website call at runtime.
    ///   Discord     - the hosts the desktop client and CDN use.
    ///   Blocked set - citizenlab/test-lists, the list OONI measurements are built on.
    ///   Controls    - large sites that no censor in our target regions blocks.
    ///
    /// A failure here always means the transport failed: refused, reset, timed out or
    /// interrupted mid-handshake. An HTTP error such as 403 or 404 counts as success,
    /// because reaching the server is what we are measuring.
    /// </summary>
    static class ProbeList
    {
        const string RobloxClient = "Roblox client";
        const string RobloxWeb = "Roblox site";
        const string RobloxCdn = "Roblox CDN";
        const string Discord = "Discord";
        const string Social = "Social";
        const string Games = "Games";
        const string Media = "Media";
        const string Reference = "Reference";
        const string Control = "Control";

        static readonly ProbeHost[] Catalog =
        {
            // -- Roblox: what the installer and the running client call ------------
            // A block on any of these is invisible on the website and fatal in the app.
            // setup.roblox.com is deliberately absent. It is a CNAME straight to
            // s3.amazonaws.com, so over HTTPS it serves Amazon's own certificate for
            // s3.amazonaws.com and the name never matches. That failure is Roblox's
            // configuration, not interference, and no setting can fix it — as a probe it
            // would report "blocked" forever and drag down every candidate's score.
            // setup.rbxcdn.com serves the same installer files under a valid certificate.
            new ProbeHost("setup.rbxcdn.com",             RobloxClient, false),
            new ProbeHost("clientsettings.roblox.com",    RobloxClient, false),
            new ProbeHost("clientsettingscdn.roblox.com", RobloxClient, false),
            new ProbeHost("clientsettings.api.roblox.com",RobloxClient, false),
            new ProbeHost("versioncompatibility.api.roblox.com", RobloxClient, false),
            new ProbeHost("gamejoin.roblox.com",          RobloxClient, false),
            new ProbeHost("assetgame.roblox.com",         RobloxClient, false),
            new ProbeHost("assetdelivery.roblox.com",     RobloxClient, false),
            new ProbeHost("ecsv2.roblox.com",             RobloxClient, false),
            new ProbeHost("textfilter.roblox.com",        RobloxClient, false),
            new ProbeHost("voice.roblox.com",             RobloxClient, false),
            new ProbeHost("metrics.roblox.com",           RobloxClient, false),

            // -- Roblox: what the website and its pages call ------------------------
            new ProbeHost("www.roblox.com",               RobloxWeb, false),
            new ProbeHost("web.roblox.com",               RobloxWeb, false),
            new ProbeHost("apis.roblox.com",              RobloxWeb, false),
            new ProbeHost("auth.roblox.com",              RobloxWeb, false),
            new ProbeHost("users.roblox.com",             RobloxWeb, false),
            new ProbeHost("games.roblox.com",             RobloxWeb, false),
            new ProbeHost("thumbnails.roblox.com",        RobloxWeb, false),
            new ProbeHost("friends.roblox.com",           RobloxWeb, false),
            new ProbeHost("presence.roblox.com",          RobloxWeb, false),
            new ProbeHost("avatar.roblox.com",            RobloxWeb, false),
            new ProbeHost("inventory.roblox.com",         RobloxWeb, false),
            new ProbeHost("catalog.roblox.com",           RobloxWeb, false),
            new ProbeHost("economy.roblox.com",           RobloxWeb, false),
            new ProbeHost("badges.roblox.com",            RobloxWeb, false),
            new ProbeHost("groups.roblox.com",            RobloxWeb, false),
            new ProbeHost("notifications.roblox.com",     RobloxWeb, false),
            new ProbeHost("realtime.roblox.com",          RobloxWeb, false),
            new ProbeHost("chat.roblox.com",              RobloxWeb, false),
            new ProbeHost("captcha.roblox.com",           RobloxWeb, false),
            new ProbeHost("roblox-api.arkoselabs.com",    RobloxWeb, false),

            // -- Roblox: images and static content ----------------------------------
            new ProbeHost("t0.rbxcdn.com",                RobloxCdn, false),
            new ProbeHost("t1.rbxcdn.com",                RobloxCdn, false),
            new ProbeHost("tr.rbxcdn.com",                RobloxCdn, false),
            new ProbeHost("c0.rbxcdn.com",                RobloxCdn, false),
            new ProbeHost("images.rbxcdn.com",            RobloxCdn, false),
            new ProbeHost("js.rbxcdn.com",                RobloxCdn, false),
            new ProbeHost("static.rbxcdn.com",            RobloxCdn, false),

            // -- Discord -------------------------------------------------------------
            new ProbeHost("discord.com",                  Discord, false),
            new ProbeHost("gateway.discord.gg",           Discord, false),
            new ProbeHost("cdn.discordapp.com",           Discord, false),
            new ProbeHost("media.discordapp.net",         Discord, false),
            new ProbeHost("updates.discord.com",          Discord, false),

            // -- Social and messaging ------------------------------------------------
            new ProbeHost("x.com",                        Social, false),
            new ProbeHost("pbs.twimg.com",                Social, false),
            new ProbeHost("www.instagram.com",            Social, false),
            new ProbeHost("scontent.cdninstagram.com",    Social, false),
            new ProbeHost("web.telegram.org",             Social, false),
            new ProbeHost("web.whatsapp.com",             Social, false),
            new ProbeHost("www.reddit.com",               Social, false),
            new ProbeHost("www.linkedin.com",             Social, false),
            new ProbeHost("medium.com",                   Social, false),

            // -- Games ---------------------------------------------------------------
            new ProbeHost("steamcommunity.com",           Games, false),
            new ProbeHost("store.steampowered.com",       Games, false),
            new ProbeHost("www.epicgames.com",            Games, false),
            new ProbeHost("www.ea.com",                   Games, false),

            // -- Media ---------------------------------------------------------------
            new ProbeHost("www.youtube.com",              Media, false),
            new ProbeHost("www.twitch.tv",                Media, false),
            new ProbeHost("open.spotify.com",             Media, false),
            new ProbeHost("vimeo.com",                    Media, false),
            new ProbeHost("soundcloud.com",               Media, false),

            // -- Reference and news, from the Citizen Lab test list --------------------
            new ProbeHost("tr.wikipedia.org",             Reference, false),
            new ProbeHost("rutracker.org",                Reference, false),
            new ProbeHost("www.bbc.com",                  Reference, false),
            new ProbeHost("www.amerikaninsesi.com",       Reference, false),
            new ProbeHost("odatv.com",                    Reference, false),
            new ProbeHost("t24.com.tr",                   Reference, false),
            new ProbeHost("eksisozluk.com",               Reference, false),
            new ProbeHost("chatgpt.com",                  Reference, false),

            // -- Controls: breaking one of these costs more than fixing a target -------
            new ProbeHost("www.google.com",               Control, true),
            new ProbeHost("github.com",                   Control, true),
            new ProbeHost("www.cloudflare.com",           Control, true),
            new ProbeHost("www.microsoft.com",            Control, true),
            new ProbeHost("www.apple.com",                Control, true),
            new ProbeHost("stackoverflow.com",            Control, true),
            new ProbeHost("www.bing.com",                 Control, true),
            new ProbeHost("outlook.live.com",             Control, true)
        };

        /// <summary>
        /// Everything to test. When probeDomains is set in config.ini the user's own
        /// addresses are tested as well, because no built-in list can know which site
        /// somebody actually needs.
        /// </summary>
        public static List<ProbeHost> Build()
        {
            List<ProbeHost> hosts = new List<ProbeHost>();
            List<string> seen = new List<string>();

            foreach (string custom in Settings.CustomProbeDomains)
            {
                string host = custom.Trim().ToLowerInvariant();
                if (host.Length == 0 || seen.Contains(host)) continue;
                seen.Add(host);
                hosts.Add(new ProbeHost(host, Strings.Get("probe.yours"), false));
            }

            foreach (ProbeHost entry in Catalog)
            {
                if (seen.Contains(entry.Host)) continue;
                seen.Add(entry.Host);
                hosts.Add(entry);
            }

            return hosts;
        }

        /// <summary>
        /// Groups a person can choose to cover, in display order. Controls are left out:
        /// they exist to catch a setting that breaks ordinary browsing, and are not
        /// something anybody would ask to have the bypass applied to.
        /// </summary>
        public static List<string> SelectableGroups()
        {
            List<string> groups = new List<string>();

            foreach (ProbeHost host in Build())
            {
                if (host.IsControl) continue;
                if (!groups.Contains(host.Group)) groups.Add(host.Group);
            }

            return groups;
        }

        /// <summary>Every address belonging to one group.</summary>
        public static List<string> HostsInGroup(string group)
        {
            List<string> hosts = new List<string>();

            foreach (ProbeHost host in Build())
                if (host.Group == group && !host.IsControl) hosts.Add(host.Host);

            return hosts;
        }

        /// <summary>
        /// The group's name as a person should read it.
        ///
        /// The internal names are English and structural — "Roblox client" versus
        /// "Roblox site" describes where an address is called from, which is the right
        /// distinction for the catalogue and a useless one to show somebody deciding what
        /// to cover. They are also what is written to config.ini, so they stay as they
        /// are and the label is looked up separately.
        /// </summary>
        public static string DisplayName(string group)
        {
            return Strings.Get(LabelKey(group, "name"));
        }

        /// <summary>
        /// What is actually inside the group, named. "Social, 9 addresses" tells nobody
        /// whether the thing they care about is in there.
        /// </summary>
        public static string Examples(string group)
        {
            return Strings.Get(LabelKey(group, "examples"));
        }

        static string LabelKey(string group, string suffix)
        {
            string key = group.ToLowerInvariant().Replace(" ", "");
            return "group." + key + "." + suffix;
        }

        /// <summary>Group names in the order they should appear in a report.</summary>
        public static List<string> GroupsOf(IList<ProbeHost> hosts)
        {
            List<string> groups = new List<string>();
            foreach (ProbeHost host in hosts)
                if (!groups.Contains(host.Group)) groups.Add(host.Group);
            return groups;
        }
    }
}
