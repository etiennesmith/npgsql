﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;

namespace Npgsql.BackendMessages
{
    class DataRowNonSequentialMessage : DataRowMessage
    {
        List<int> _columnOffsets;
        int _endOffset;
        /// <summary>
        /// List of all streams that have been opened on this row, and need to be disposed of when the row
        /// is consumed.
        /// </summary>
        List<IDisposable> _streams;

        internal override DataRowMessage Load(NpgsqlBuffer buf)
        {
            NumColumns = buf.ReadInt16();
            Buffer = buf;
            Column = -1;
            ColumnLen = -1;
            PosInColumn = 0;
            // TODO: Recycle message objects rather than recreating for each row
            _columnOffsets = new List<int>(NumColumns);
            for (var i = 0; i < NumColumns; i++)
            {
                _columnOffsets.Add(buf.ReadPosition);
                var len = buf.ReadInt32();
                if (len != -1)
                {
                    buf.Seek(len, SeekOrigin.Current);
                }
            }
            _endOffset = buf.ReadPosition;
            return this;
        }

        internal override void SeekToColumn(int column)
        {
            CheckColumnIndex(column);

            if (Column != column)
            {
                Buffer.Seek(_columnOffsets[column], SeekOrigin.Begin);
                Column = column;
                ColumnLen = Buffer.ReadInt32();
                PosInColumn = 0;
            }
        }

        internal override void SeekInColumn(int posInColumn)
        {
            if (posInColumn > ColumnLen) {
                posInColumn = ColumnLen;
            }

            Buffer.Seek(_columnOffsets[Column] + 4 + posInColumn, SeekOrigin.Begin);
            PosInColumn = posInColumn;
        }

        internal override Stream GetStream()
        {
            Contract.Requires(PosInColumn == 0);
            var s = Buffer.GetMemoryStream(ColumnLen);
            if (_streams == null) {
                _streams = new List<IDisposable>();
            }
            _streams.Add(s);
            return s;
        }

        internal override void Consume()
        {
            Buffer.Seek(_endOffset, SeekOrigin.Begin);
            if (_streams != null)
            {
                foreach (var stream in _streams) {
                    stream.Dispose();
                }
                _streams.Clear();
            }
        }
    }
}
