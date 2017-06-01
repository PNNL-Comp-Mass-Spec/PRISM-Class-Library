using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PRISM
{
    class clsProcessorCoreInfo
    {
        /// <summary>
        /// Processor ID for this core
        /// </summary>
        public int ProcessorID { get; }

        /// <summary>
        /// Physical ID of the core
        /// </summary>
        public int PhysicalID { get; set; }

        /// <summary>
        /// Core ID of the core
        /// </summary>
        public int CoreID { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="processorID"></param>
        public clsProcessorCoreInfo(int processorID)
        {
            ProcessorID = processorID;
        }
    }
}
