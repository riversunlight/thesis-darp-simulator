using System;
using System.Collections.Generic;
using System.Text;

namespace Simulator.MySearchAlgorithm
{
    public class RouteStep
    {
        public int NodeIndex { get; set; }
        public int RequestID => NodeIndex / 2;
        public int PickupOrDelivery => NodeIndex % 2;
        public int ArrivalTime {  get; set; }
        public int DepatureTime { get; set; }
        public int CumulativeLoad {  get; set; }

        public RouteStep(int Id)
        {
            NodeIndex = Id;
            ArrivalTime = -1;
            DepatureTime = -1;
            CumulativeLoad = -1;
        }

        public RouteStep(RouteStep old)
        {
            NodeIndex = old.NodeIndex;
            ArrivalTime = old.ArrivalTime;
            DepatureTime = old.DepatureTime;
            CumulativeLoad = old.CumulativeLoad;
        }
    }
}
