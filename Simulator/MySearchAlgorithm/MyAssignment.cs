using System;
using System.Collections.Generic;
using System.Text;
using Simulator.MySearchAlgorithm;
using Simulator.Objects.Data_Objects.Routing;
using Simulator.Objects.Data_Objects.Simulation_Data_Objects;
using Simulator.Objects.Data_Objects.Simulation_Objects;

namespace Simulator.MySearchAlgorithm
{
    public class MyAssignment
    {
        public Dictionary<int, List<RouteStep>> VehicleRoutes { get; set; }
        public Dictionary<int, int> NodeToVehicleMap { get; set; }

        public Dictionary<int, long> ObjectiveFunctions;
        public List<Customer> Customers;
        public List<RouteStep> LastStep;
        public int[] gene;
        public MyAssignment(int customer_num)
        {
            VehicleRoutes = new Dictionary<int, List<RouteStep>>();
            NodeToVehicleMap = new Dictionary<int, int>();
            ObjectiveFunctions = new Dictionary<int, long>();
            Customers = new List<Customer>();
            LastStep  = new List<RouteStep>();
            gene = new int[customer_num * 2];
        }

        // convert solution'sIndex to Index of List<Stop> Stops
        public int ConvertIndex2LStopId(int vehicle_count, int index)
        {
            return 2 * vehicle_count + index;
        }

        public void SetObjects()
        {
            ObjectiveFunctions[0] = ObjectiveFunctionRoutingLengh();
            ObjectiveFunctions[1] = ObjectiveFunctionTotalDelay();
        }


        public int CalculateProcessingTime(int currentStep)
        {
            return 0; // お客さんの待ち時間を考慮したい。
        }

        public void Simulate(RoutingDataModel DataModel)
        {
            Customers = new List<Customer>();
            foreach (var customer in DataModel.IndexManager.Customers)
            {
                Customers.Add(new Customer(customer));
            }
            VehicleRoutes[0] = new List<RouteStep>();
            for (int i = 0; i < 2 * Customers.Count; i++)
            {
                if (gene[i] == -1) continue;
                VehicleRoutes[0].Add(new RouteStep(gene[i]));
            }
            foreach (var vehicleRoute in VehicleRoutes)
            {
                int vehicleId = vehicleRoute.Key;
                List<RouteStep> routeSteps = vehicleRoute.Value;
                int currentTime = 0; // シミュレーション開始時刻
                int currentLoad = 0; // 現在の車両の負荷
                int previousStopId = DataModel.Starts[vehicleId];

                for (int i = 0; i < routeSteps.Count; i++)
                {
                    RouteStep currentStep = routeSteps[i];

                    // 到着時刻の計算
                    int customerIndex = currentStep.RequestID;
                    int pickupDelivery = currentStep.PickupOrDelivery;
                    //int nextStopId = DataModel.IndexManager.Stops[ConvertIndex2LStopId(VehicleRoutes.Count, currentStep.NodeIndex)].Id;


                    int travelTime = (int)DataModel.TravelTimes[previousStopId, ConvertIndex2LStopId(VehicleRoutes.Count, currentStep.NodeIndex)];
                    currentTime += travelTime;
                    currentStep.ArrivalTime = currentTime;

                    // 停留所での処理時間を計算（例として固定値を使用）
                    long desiredPickupTime = Customers[customerIndex].DesiredTimeWindow[pickupDelivery];
                    if (desiredPickupTime > currentTime)
                    {
                        currentTime = (int)desiredPickupTime;
                    }
                    currentStep.DepatureTime = currentTime;

                    // 累積負荷の計算など
                    if (currentStep.NodeIndex % 2 == 0)
                    {
                        Customers[customerIndex].RealTimeWindow[0] = currentTime;
                        currentLoad += 1; // ピックアップの場合
                    }
                    else
                    {
                        Customers[customerIndex].RealTimeWindow[1] = currentTime;
                        currentLoad -= 1; // デリバリーの場合
                    }
                    currentStep.CumulativeLoad = currentLoad;

                    // 時間を進める
                    currentTime = currentStep.DepatureTime;
                }

                LastStep.Add(new RouteStep(vehicleId));
                int backTime = (int)DataModel.TravelTimes[previousStopId, DataModel.Ends[vehicleId]];
                currentTime += backTime;
                LastStep[LastStep.Count - 1].ArrivalTime = currentTime;

            }
            SetObjects();
        }

        public long ObjectiveFunctionRoutingLengh()
        {
            long res = 0;
            for (int vehicleId = 0;  vehicleId < VehicleRoutes.Count; vehicleId++)
            {
                res += LastStep[vehicleId].ArrivalTime;
            }
            return res;
        }
        public long ObjectiveFunctionTotalDelay()
        {
            long res = 0;
            for (int customerId = 0; customerId < Customers.Count; customerId++)
            {
                res += Customers[customerId].DelayTime;
            }
            return res;
        }
    }

    
}
