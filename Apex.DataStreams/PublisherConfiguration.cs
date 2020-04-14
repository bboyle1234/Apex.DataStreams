using System;
using System.Collections.Generic;
using System.Text;

namespace Apex.DataStreams {

    public class PublisherConfiguration {

        public Schema Schema { get; set; }

        public int ListenPort { get; set; }
    }
}
