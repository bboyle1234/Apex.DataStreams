using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace Apex.DataStreams {

    public sealed class Schema {

        readonly Dictionary<Type, byte> ByMessageType;
        readonly Dictionary<byte, Type> ByMessageTypeCode;

        internal Schema(params (byte messageTypeCode, Type messageType)[] items) {
            ByMessageType = items.ToDictionary(x => x.messageType, x => x.messageTypeCode);
            ByMessageTypeCode = items.ToDictionary(x => x.messageTypeCode, x => x.messageType);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Type GetMessageType(byte messageTypeCode)
            => ByMessageTypeCode[messageTypeCode];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte GetMessageTypeCode(Type messageType)
            => ByMessageType[messageType];

        public static SchemaBuilder Create()
            => new SchemaBuilder();
    }
}
