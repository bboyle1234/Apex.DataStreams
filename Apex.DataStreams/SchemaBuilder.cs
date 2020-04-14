using Apex.DataStreams.AdminMessages;
using Nito.AsyncEx;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace Apex.DataStreams {

    public sealed class SchemaBuilder : IEnumerable {

        readonly List<(byte messageTypeCode, Type messageType)> Messages = new List<(byte messageTypeCode, Type messageType)>();

        public SchemaBuilder() {
            Add(255, typeof(HeartBeat));
        }

        public SchemaBuilder Add(byte messageTypeCode, Type messageType) {
            if (Messages.Any(m => m.messageTypeCode == messageTypeCode)) throw new Exception($"Message Type Code '{messageTypeCode}' already exists.");
            if (Messages.Any(m => m.messageType == messageType)) throw new Exception($"Message Type '{messageType}' already exists.");
            Messages.Add((messageTypeCode, messageType));
            return this;
        }

        public IEnumerator GetEnumerator()
            => throw new NotImplementedException();

        public Schema Build()
            => new Schema(Messages.ToArray());

        public static implicit operator Schema(SchemaBuilder builder)
            => builder.Build();
    }
}
