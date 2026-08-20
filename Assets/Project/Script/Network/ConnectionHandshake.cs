using System.Collections.Generic;
using System.Text;
using Office.Core;
using Office.Data;
using UnityEngine;

namespace Office.Network
{
    public static class ConnectionHandshake
    {
        public const string MismatchReason = "BUILD MISMATCH — the host is running different content.";

        public const string RunInProgressReason = "SHIFT IN PROGRESS — wait for the run to end.";

        public static string Build()
        {
            var definitions = ServiceLocator.TryGet<DefinitionRegistry>(out var registry)
                ? registry.All
                : null;

            return Build(Application.version, definitions);
        }

        public static string Build(string version, IReadOnlyList<ContentDefinition> definitions) =>
            $"{version}|{ContentFingerprint(definitions):X8}";

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
