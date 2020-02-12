// ReSharper disable once CheckNamespace
namespace PRISM
{
    class ProcessorCoreInfo
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
        public ProcessorCoreInfo(int processorID)
        {
            ProcessorID = processorID;
        }
    }
}
