using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Apex.DataStreams.Tests {

    [TestClass]
    public class AssemblyInitialize {
		
		[AssemblyInitialize]
        public static void Initialize(TestContext context) {

        }

        [AssemblyCleanup]
        public static void AssemblyCleanup() {

        }
    }
}
