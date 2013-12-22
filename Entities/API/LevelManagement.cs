﻿using Neon.Entities.Implementation.Content;
using Neon.Entities.Implementation.Runtime;
using Neon.Entities.Implementation.Shared;
using Neon.Utilities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace Neon.Entities {
    /// <summary>
    /// Facilitates the creation, saving, and loading of snapshots and template groups. This is the
    /// API point that all serialization occurs in.
    /// </summary>
    public static class LevelManager {
        /// <summary>
        /// Returns an empty game snapshot.
        /// </summary>
        public static IGameSnapshot CreateSnapshot() {
            return new GameSnapshot();
        }

        /// <summary>
        /// Returns an empty template group.
        /// </summary>
        public static ITemplateGroup CreateTemplateGroup() {
            return new TemplateGroup();
        }

        /// <summary>
        /// Converts a game snapshot to JSON that can be restored later.
        /// </summary>
        public static string SaveSnapshot(IGameSnapshot snapshot) {
            return SerializationHelpers.Serialize<GameSnapshot>((GameSnapshot)snapshot,
                RequiredConverters.GetConverters(),
                RequiredConverters.GetContextObjects(Maybe<GameEngine>.Empty));
        }

        /// <summary>
        /// Converts a template group to JSON that can be restored later.
        /// </summary>
        public static string SaveTemplates(ITemplateGroup templates) {
            return SerializationHelpers.Serialize<TemplateGroup>((TemplateGroup)templates,
                RequiredConverters.GetConverters(),
                RequiredConverters.GetContextObjects(Maybe<GameEngine>.Empty));
        }

        /// <summary>
        /// Loads an IGameSnapshot from the given JSON and the given template group. The JSON should
        /// have been generated by calling SaveSnapshot.
        /// </summary>
        public static IGameSnapshot LoadSnapshot(string snapshotJson, string templateJson) {
            return GameSnapshotRestorer.Restore(snapshotJson, templateJson, Maybe<GameEngine>.Empty);
        }

        /// <summary>
        /// Loads an ITemplateGroup from the given JSON. The JSON should have been generated by
        /// calling SaveTemplates.
        /// </summary>
        public static ITemplateGroup LoadTemplates(string json) {
            return SerializationHelpers.Deserialize<TemplateGroup>(json,
                RequiredConverters.GetConverters(),
                RequiredConverters.GetContextObjects(Maybe<GameEngine>.Empty));
        }
    }
}