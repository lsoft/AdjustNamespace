using AdjustNamespace.Adjusting.Plan;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AdjustNamespace.Adjusting.Report
{
    /// <summary>
    /// What a run has done with one of the files the user wanted to adjust.
    /// </summary>
    [DebuggerDisplay("{Status}: {FilePath}")]
    public sealed class AdjustFileOutcome
    {
        private static readonly IReadOnlyList<NamespaceChange> NoChanges = Array.Empty<NamespaceChange>();

        /// <summary>
        /// Full path to the file.
        /// </summary>
        public string FilePath
        {
            get;
        }

        /// <summary>
        /// What has happened with the file.
        /// </summary>
        public AdjustFileStatus Status
        {
            get;
        }

        /// <summary>
        /// The namespaces of the file before and after the run (an <see cref="AdjustFileStatus.Adjusted"/>
        /// file only).
        /// </summary>
        public IReadOnlyList<NamespaceChange> Changes
        {
            get;
        }

        /// <summary>
        /// Why the file is blocked (a <see cref="AdjustFileStatus.Blocked"/> file only).
        /// </summary>
        public AdjustBlock? Block
        {
            get;
        }

        /// <summary>
        /// The reason of a skip, or the message of the failure; <c>null</c> for the other states.
        /// </summary>
        public string? Message
        {
            get;
        }

        /// <summary>
        /// How many edits the adjusting of this file has made in the other files of the solution
        /// (the references to the types it has moved).
        /// </summary>
        public int EditsInOtherFiles
        {
            get;
            internal set;
        }

        /// <summary>
        /// The files the adjusting of this file has changed (this file itself included) with
        /// the kinds of the changes.
        /// </summary>
        public IReadOnlyList<AdjustTouchedFile> EditedFiles
        {
            get;
            internal set;
        } = Array.Empty<AdjustTouchedFile>();

        private AdjustFileOutcome(
            string filePath,
            AdjustFileStatus status,
            IReadOnlyList<NamespaceChange> changes,
            AdjustBlock? block,
            string? message
            )
        {
            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            FilePath = filePath;
            Status = status;
            Changes = changes;
            Block = block;
            Message = message;
        }

        /// <summary>
        /// The file has been adjusted.
        /// </summary>
        public static AdjustFileOutcome Adjusted(
            string filePath,
            IReadOnlyList<NamespaceChange> changes
            )
        {
            return new AdjustFileOutcome(filePath, AdjustFileStatus.Adjusted, changes, null, null);
        }

        /// <summary>
        /// There was nothing to do with the file.
        /// </summary>
        public static AdjustFileOutcome Skipped(
            string filePath,
            string reason
            )
        {
            return new AdjustFileOutcome(filePath, AdjustFileStatus.Skipped, NoChanges, null, reason);
        }

        /// <summary>
        /// The file cannot be adjusted.
        /// </summary>
        public static AdjustFileOutcome Blocked(
            AdjustBlock block
            )
        {
            return new AdjustFileOutcome(block.FilePath, AdjustFileStatus.Blocked, NoChanges, block, block.Message);
        }

        /// <summary>
        /// The session has ended before the file was reached.
        /// </summary>
        public static AdjustFileOutcome NotProcessed(
            string filePath
            )
        {
            return new AdjustFileOutcome(filePath, AdjustFileStatus.NotProcessed, NoChanges, null, null);
        }

        /// <summary>
        /// The processing of the file has failed.
        /// </summary>
        public static AdjustFileOutcome Failed(
            string filePath,
            string message
            )
        {
            return new AdjustFileOutcome(filePath, AdjustFileStatus.Failed, NoChanges, null, message);
        }
    }
}
