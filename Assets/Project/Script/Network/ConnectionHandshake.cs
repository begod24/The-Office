using System.Collections.Generic;
using System.Text;
using Office.Core;
using Office.Data;
using UnityEngine;

namespace Office.Network
{
    /// <summary>
    /// The string a joining client must present, and the server must recognise.
    /// </summary>
    /// <remarks>
    /// Two clients with different content disagree about what an id means, and every id in
    /// this project is meaningless without the registry that defines it. That mismatch has
    /// no symptom on the wire: the join succeeds, and then one player picks up a stapler and
    /// the other watches them hold a coffee cup. Comparing a fingerprint of the content at
    /// connection time turns a whole class of silent desync into one clear rejection.
    /// </remarks>
    public static class ConnectionHandshake
    {
        /// <summary>What the server sends back when it turns a client away.</summary>
        public const string MismatchReason = "BUILD MISMATCH — the host is running different content.";

        /// <summary>The handshake for this build, from the registry currently registered.</summary>
        public static string Build()
        {
            var definitions = ServiceLocator.TryGet<DefinitionRegistry>(out var registry)
                ? registry.All
                : null;

            return Build(Application.version, definitions);
        }

        public static string Build(string version, IReadOnlyList<ContentDefinition> definitions) =>
            $"{version}|{ContentFingerprint(definitions):X8}";

        /// <summary>
        /// A fingerprint of every id and the name behind it.
        /// </summary>
        /// <remarks>
        /// Ids alone are not enough: two builds can both hand out ids 1..12 and disagree about
        /// which asset each one is. The name goes in so that a renamed or replaced definition
        /// changes the fingerprint too.
        /// </remarks>
        public static uint ContentFingerprint(IReadOnlyList<ContentDefinition> definitions)
        {
            if (definitions == null || definitions.Count == 0) return Fnv1a("empty");

            var builder = new StringBuilder(definitions.Count * 24);

            foreach (var definition in definitions)
            {
                if (definition == null) continue;

                builder.Append(definition.Id).Append(':').Append(definition.name).Append(';');
            }

            return Fnv1a(builder.ToString());
        }

        /// <summary>
        /// FNV-1a, 32 bit.
        /// </summary>
        /// <remarks>
        /// Written out rather than calling <c>string.GetHashCode</c> because that is not
        /// stable across runtimes or, on some, across processes — the two machines being
        /// compared here are by definition not the same process. A hash that disagrees for
        /// reasons unrelated to content would reject every join with an identical build,
        /// which is a worse failure than the one this guards against.
        /// </remarks>
        public static uint Fnv1a(string value)
        {
            unchecked
            {
                const uint offset = 2166136261;
                const uint prime = 16777619;

                var hash = offset;

                if (value == null) return hash;

                foreach (var character in value)
                {
                    hash ^= character;
                    hash *= prime;
                }

                return hash;
            }
        }
    }
}
