﻿using System;

// This file contains some exception definitions that Neon.Serialization uses.

namespace Neon.Serialization {
    /// <summary>
    /// Exception thrown when a parsing error has occurred.
    /// </summary>
    public class ParseException : Exception {
        /// <summary>
        /// Helper method to create a parsing exception message
        /// </summary>
        private static string CreateMessage(string message, Parser context) {
            int start = Math.Max(0, context._start - 10);
            int length = Math.Min(20, context._input.Length - start);

            return "Error while parsing : " + message + "; context = \"" +
                context._input.Substring(start, length) + "\"";
        }

        /// <summary>
        /// Initializes a new instance of the ParseException class.
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="context">The context that the error occurred</param>
        public ParseException(string message, Parser context)
            : base(CreateMessage(message, context)) {
        }
    }

    /// <summary>
    /// Exception thrown when a type that was imported/exported requires a custom converter, but one
    /// was not registered.
    /// </summary>
    public sealed class RequiresCustomConverterException : Exception {
        private static string CreateMessage(Type type, bool importing) {
            return "The given type " + type + " requires a custom " +
                (importing ? "importer" : "exporter") + " (based on annotations), but one was " +
                "not found.";
        }

        internal RequiresCustomConverterException(Type type, bool importing)
            : base(CreateMessage(type, importing)) {
        }
    }
}