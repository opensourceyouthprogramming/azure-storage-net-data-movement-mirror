//------------------------------------------------------------------------------
// <copyright file="CloudFileReader.cs" company="Microsoft">
//    Copyright (c) Microsoft Corporation
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.WindowsAzure.Storage.DataMovement.TransferControllers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage.File;

    class CloudFileReader : RangeBasedReader
    {
        private CloudFile file;

        public CloudFileReader(
            TransferScheduler scheduler,
            SyncTransferController controller,
            CancellationToken cancellationToken)
            :base(scheduler, controller, cancellationToken)
        {
            this.file = this.SharedTransferData.TransferJob.Source.AzureFile;
            Debug.Assert(null != this.file, "Initializing a CloudFileReader, the source location should be a CloudFile instance.");
        }

        protected override async Task DoFetchAttributesAsync()
        {         
            await this.file.FetchAttributesAsync(
                null,
                Utils.GenerateFileRequestOptions(this.Location.FileRequestOptions),
                Utils.GenerateOperationContext(this.Controller.TransferContext),
                this.CancellationToken);

            if (string.IsNullOrEmpty(this.Location.ETag))
            {
                if ((0 != this.SharedTransferData.TransferJob.CheckPoint.EntryTransferOffset)
                    || (this.SharedTransferData.TransferJob.CheckPoint.TransferWindow.Any()))
                {
                    throw new InvalidOperationException(Resources.RestartableInfoCorruptedException);
                }

                this.Location.ETag = this.Location.AzureFile.Properties.ETag;
            }
            else if ((this.SharedTransferData.TransferJob.CheckPoint.EntryTransferOffset > this.Location.AzureFile.Properties.Length)
                || (this.SharedTransferData.TransferJob.CheckPoint.EntryTransferOffset < 0))
            {
                throw new InvalidOperationException(Resources.RestartableInfoCorruptedException);
            }

            this.SharedTransferData.DisableContentMD5Validation =
                null != this.Location.FileRequestOptions ?
                this.Location.FileRequestOptions.DisableContentMD5Validation.HasValue ?
                this.Location.FileRequestOptions.DisableContentMD5Validation.Value : false : false;

            this.SharedTransferData.Attributes = Utils.GenerateAttributes(this.file);
            this.SharedTransferData.TotalLength = this.file.Properties.Length;
            this.SharedTransferData.SourceLocation = this.file.Uri.ToString();
        }

        protected override async Task<List<Range>> DoGetRangesAsync(RangesSpan rangesSpan)
        {
            List<Range> rangeList = new List<Range>();

            foreach (var fileRange in await this.file.ListRangesAsync(
                     rangesSpan.StartOffset,
                     rangesSpan.EndOffset - rangesSpan.StartOffset + 1,
                     null,
                     Utils.GenerateFileRequestOptions(this.Location.FileRequestOptions),
                     Utils.GenerateOperationContext(this.Controller.TransferContext),
                     this.CancellationToken))
            {
                rangeList.Add(new Range()
                {
                    StartOffset = fileRange.StartOffset,
                    EndOffset = fileRange.EndOffset,
                    HasData = true
                });
            }

            return rangeList;
        }

        protected override async Task DoDownloadRangeToStreamAsync(RangeBasedDownloadState asyncState)
        {
            await this.Location.AzureFile.DownloadRangeToStreamAsync(
                asyncState.DownloadStream,
                asyncState.StartOffset,
                asyncState.Length,
                null,
                Utils.GenerateFileRequestOptions(this.Location.FileRequestOptions),
                Utils.GenerateOperationContext(this.Controller.TransferContext),
                this.CancellationToken);
        }
    }
}
