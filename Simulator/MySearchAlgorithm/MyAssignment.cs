using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Simulator.MySearchAlgorithm;
using Simulator.Objects.Data_Objects.Routing;
using Simulator.Objects.Data_Objects.Simulation_Data_Objects;
using Simulator.Objects.Data_Objects.Simulation_Objects;
using System.Runtime.Versioning;
using System.Linq;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Simulator.MySearchAlgorithm
{
    public class MyAssignment
    {
        public Dictionary<int, List<RouteStep>> VehicleRoutes { get; set; }
        public Dictionary<int, int> NodeToVehicleMap { get; set; }

        public Dictionary<int, long> ObjectiveFunctions;
        public Dictionary<int, long> Indicators;

        public List<Customer> Customers;
        public List<RouteStep> LastStep;
        public int[] gene;
        public int myEvalCnt;
        public int Id;
        public static string path;
        public static string ObjCase;
        public static int evalCnt;
        public static int geneCnt;
        public static int First;
        public MyAssignment(int customer_num)
        {
            VehicleRoutes = new Dictionary<int, List<RouteStep>>();
            NodeToVehicleMap = new Dictionary<int, int>();
            ObjectiveFunctions = new Dictionary<int, long>();

            Indicators = new Dictionary<int, long>();
            Customers = new List<Customer>();
            LastStep  = new List<RouteStep>();
            gene = new int[customer_num * 2];
            myEvalCnt = -1;
        }


        public MyAssignment(MyAssignment old)
        {
            VehicleRoutes = old.VehicleRoutes.ToDictionary(
                pair=>pair.Key,
                pair=>new List<RouteStep>(pair.Value)
            );
            NodeToVehicleMap = new Dictionary<int, int>(old.NodeToVehicleMap);
            ObjectiveFunctions = new Dictionary<int, long>(old.ObjectiveFunctions);
            Indicators = new Dictionary<int, long>(old.Indicators);

            Customers = new List<Customer>(old.Customers);
            LastStep = new List<RouteStep>(old.LastStep);
            gene = new int[Customers.Count * 2];
            for (int i = 0; i < gene.Length; i++)
            {
                gene[i] = old.gene[i];
            }
            myEvalCnt = old.myEvalCnt;
            Id = old.Id;
        }

        public void resetEvalCnt()
        {
            evalCnt = 0;
        }
        public void setGeneCnt(int cnt)
        {
            geneCnt = cnt;
        }
        public void setPath(string _path)
        {
            resetEvalCnt();
            path = _path;
            First = 1;
            
        }
        public void setObjCase(string _objCase)
        {
            ObjCase = _objCase;

        }

        public void WirteFirstLine()
        {
            using (StreamWriter writer = new StreamWriter(path, append: false))
            {
                string tmp = "ID, Evaluation,Generation";
                for (int i = 0; i < ObjectiveFunctions.Count; i++)
                {
                    tmp += ",Object" + i;
                }
                for (int i = 0; i < gene.Length; i++)
                {
                    tmp += ",Gene" + i;
                }
                for (int i = 0; i < Indicators.Count; i++)
                {
                    tmp += ",Indicator" + i;
                }
                writer.WriteLine(tmp);
            }

        }

        // convert solution'sIndex to Index of List<Stop> Stops
        public int ConvertIndex2LStopId(int vehicle_count, int index)
        {
            return 2 * vehicle_count + index;
        }

        public void SetObjects()
        {
            switch (ObjCase)
            {
                case "GA_TD":
                    ObjectiveFunctions[0] = ObjectiveFunctionRouteLength();
                    break;
                case "GA_TDT":
                    ObjectiveFunctions[0] = ObjectiveFunctionTotalDelay();
                    break;
                case "GA_HALF":
                    ObjectiveFunctions[0] = (long)Math.Round(0.5 * ObjectiveFunctionTotalDelay() + 0.5 * ObjectiveFunctionRouteLength());
                    break;
                case "NSGA_TDTDT":
                    ObjectiveFunctions[0] = ObjectiveFunctionRouteLength();
                    ObjectiveFunctions[1] = ObjectiveFunctionTotalDelay();
                    break;
                case "NSGA_TDCUML":
                    ObjectiveFunctions[0] = ObjectiveFunctionRouteLength();
                    ObjectiveFunctions[1] = ObjectiveFunctionMaxCumulativeLoad();
                    break;
                case "NSGA_TDTCUML":
                    ObjectiveFunctions[0] = ObjectiveFunctionTotalDelay();
                    ObjectiveFunctions[1] = ObjectiveFunctionMaxCumulativeLoad();
                    break;
                default:
                    ObjectiveFunctions[0] = ObjectiveFunctionFinishTime();
                    ObjectiveFunctions[1] = ObjectiveFunctionDryRun();
                    break;
            }
        }

        public void SetIndicators()
        {
            Indicators[0] = ObjectiveFunctionRouteLength();
            Indicators[1] = ObjectiveFunctionTotalWait();
            Indicators[2] = ObjectiveFunctionTotalDelay();
            Indicators[3] = ObjectiveFunctionDryRun();
            Indicators[4] = ObjectiveFunctionFinishTime();
            Indicators[5] = ObjectiveFunctionVehicleWait();
            Indicators[6] = ObjectiveFunctionMaxCumulativeLoad();
        }


        public int CalculateProcessingTime(int currentStep)
        {
            return 0; // お客さんの待ち時間を考慮したい。
        }

        public void Simulate(RoutingDataModel DataModel)
        {
            evalCnt++;
            myEvalCnt = evalCnt;
            Customers = new List<Customer>();
            foreach (var customer in DataModel.IndexManager.Customers)
            {
                Customers.Add(new Customer(customer));
            }
            int[] alreadyGetOn = new int[Customers.Count];
            int vehicleCnt = 0;
            VehicleRoutes[vehicleCnt] = new List<RouteStep>();
            for (int i = 0; i < gene.Length; i++)
            {
                if (gene[i] == -1)
                {
                    if (i != 0 && gene[i - 1] != -1 && i != gene.Length - 1 && gene[i + 1] != -1) {
                        vehicleCnt++;
                        VehicleRoutes[vehicleCnt] = new List<RouteStep>();
                    }
                    continue;
                }
                VehicleRoutes[vehicleCnt].Add(new RouteStep(gene[i]));
            }

            for (int i = 0; i < Customers.Count; i++)
            {
                int convertStopId = ConvertIndex2LStopId(VehicleRoutes.Count, 2*i);
                int convertStopId2 = ConvertIndex2LStopId(VehicleRoutes.Count, 2 * i + 1);

                int travelTime = (int)DataModel.TravelTimes[convertStopId, convertStopId2];
                //Debug.Assert(travelTime == Customers[i].DesiredTimeWindow[1] - Customers[i].DesiredTimeWindow[0]);
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

                    int convertStopId = ConvertIndex2LStopId(VehicleRoutes.Count, currentStep.NodeIndex);
                    int travelTime = (int)DataModel.TravelTimes[previousStopId, convertStopId];
                    currentTime += travelTime;
                    currentStep.ArrivalTime = currentTime;

                    //if (pickupDelivery == 0) // 乗車なら希望時刻まで待機
                    //{
                        long desiredPickupTime = Customers[customerIndex].DesiredTimeWindow[pickupDelivery];
                        if (desiredPickupTime > currentTime)
                        {
                            currentTime = (int)desiredPickupTime;
                        }
                        currentStep.DepatureTime = currentTime;
                    //}

                    // 累積負荷の計算など
                    if (currentStep.NodeIndex % 2 == 0)
                    {
                        Customers[customerIndex].RealTimeWindow[0] = currentTime;
                        currentLoad += 1; // ピックアップの場合
                        alreadyGetOn[customerIndex] = 1;
                    }
                    else
                    {
                        if (alreadyGetOn[customerIndex] == 0)
                        {
                            Debug.Assert(false);
                        }
                        Customers[customerIndex].RealTimeWindow[1] = currentTime;
                        currentLoad -= 1; // デリバリーの場合
                    }
                    currentStep.CumulativeLoad = currentLoad;

                    // 時間を進める
                    currentTime = currentStep.DepatureTime;
                    previousStopId = convertStopId;
                }

                LastStep.Add(new RouteStep(vehicleId));
                int backTime = (int)DataModel.TravelTimes[previousStopId, DataModel.Ends[vehicleId]];
                currentTime += backTime;
                LastStep[LastStep.Count - 1].ArrivalTime = currentTime;

            }
            SetObjects();
            SetIndicators();
        }

        public void SetId(int _id)
        {
            Id = _id;
            AppendCSVSolutionData();
        }


        // 目的関数群

        public long ObjectiveFunctionFinishTime() // デマンド達成時刻
        {
            long res = 0;
            for (int vehicleId = 0;  vehicleId < VehicleRoutes.Count; vehicleId++)
            {
                res += LastStep[vehicleId].ArrivalTime;
            }
            return res;
        }
        public long ObjectiveFunctionDryRun() // 空走距離
        {
            long res = 0;
            for (int vehicleId = 0; vehicleId < VehicleRoutes.Count; vehicleId++)
            {
                int prevCumulaative = 0;
                int prevDepatureTime = 0;
                for (int stepId = 0; stepId < VehicleRoutes[vehicleId].Count; stepId++)
                {
                    RouteStep currentStep = VehicleRoutes[vehicleId][stepId];
                    if (prevCumulaative == 0)
                    {
                        res += currentStep.ArrivalTime - prevDepatureTime;
                    }
                    prevDepatureTime = currentStep.DepatureTime;
                    prevCumulaative = currentStep.CumulativeLoad;
                }
            }
            return res;
        }

        public long ObjectiveFunctionRouteLength() // 移動時間: TD
        {
            long res = 0;
            for (int vehicleId = 0; vehicleId < VehicleRoutes.Count; vehicleId++)
            {
                int prevDepatureTime = 0;
                for (int stepId = 0; stepId < VehicleRoutes[vehicleId].Count; stepId++)
                {
                    RouteStep currentStep = VehicleRoutes[vehicleId][stepId];
                    res += currentStep.ArrivalTime - prevDepatureTime;
                    prevDepatureTime = currentStep.DepatureTime;
                }
            }
            return res;
        }

        public long ObjectiveFunctionTotalDelay() // 遅延時間: TDT
        {
            long res = 0;
            for (int customerId = 0; customerId < Customers.Count; customerId++)
            {
                res += Customers[customerId].DelayTime;
            }
            return res;
        }
        public long ObjectiveFunctionTotalWait() // 待ち時間: TWT
        {
            long res = 0;
            for (int customerId = 0; customerId < Customers.Count; customerId++)
            {
                res += Customers[customerId].WaitTime;
            }
            return res;
        }
        
        // 車両が停留所で乗客を待っている時間
        public long ObjectiveFunctionVehicleWait()
        {
            long res = 0;
            for (int vehicleId = 0; vehicleId < VehicleRoutes.Count; vehicleId++)
            {
                for (int stepId = 0; stepId < VehicleRoutes[vehicleId].Count; stepId++)
                {
                    RouteStep currentStep = VehicleRoutes[vehicleId][stepId];
                    res += currentStep.ArrivalTime - currentStep.ArrivalTime;
                }
            }
            return res;
        }

        // 車両が停留所で乗客を待っている時間(全車両の全タイミングの最大)
        public long ObjectiveFunctionMaxCumulativeLoad()
        {
            long res = 0;
            for (int vehicleId = 0; vehicleId < VehicleRoutes.Count; vehicleId++)
            {
                for (int stepId = 0; stepId < VehicleRoutes[vehicleId].Count; stepId++)
                {
                    if (VehicleRoutes[vehicleId][stepId].CumulativeLoad > res)
                    {
                        res = VehicleRoutes[vehicleId][stepId].CumulativeLoad;
                    }
                }

            }
            return res;
        }


        // アランニャ先生からアドバイスいただいた内容
        public void VisualTextSimulateResult(int Id, string directory, RoutingDataModel dataModel)
        {
            string path = directory + "/simulate" + Id + ".txt";
            string path2 = directory + "/simulate" + Id + ".csv";

            using (StreamWriter writer = new StreamWriter(path, append: false)) // append: true で追記
            {
                writer.WriteLine("Indicator:");
                for (int index = 0; index < Indicators.Count; index++)
                {
                    writer.Write(Indicators[index]);
                    if (index != Indicators.Count - 1)
                    {
                        writer.Write(",");
                    }
                }
                writer.WriteLine();

                writer.WriteLine("Vehicle Route Info:");

                for (int vehicleId = 0; vehicleId < VehicleRoutes.Count; vehicleId++)
                {
                    writer.WriteLine("VehicleID:" + vehicleId);
                    List<RouteStep> routeSteps = VehicleRoutes[vehicleId];

                    for (int stepId = 0; stepId < routeSteps.Count; stepId++)
                    {
                        RouteStep currentStep = routeSteps[stepId];
                        writer.WriteLine("stepID:" + stepId);
                        writer.WriteLine("stopID:" + Customers[currentStep.RequestID].PickupDelivery[currentStep.PickupOrDelivery].Id);
                        writer.WriteLine("requestID:" + currentStep.RequestID);
                        writer.WriteLine("PickupOrDelivery:" + currentStep.PickupOrDelivery);
                        writer.WriteLine("TimeWindow(Arrival, Depature):(" + currentStep.ArrivalTime + "," + currentStep.DepatureTime + ")");
                        writer.WriteLine("CumulativeLoad:" + currentStep.CumulativeLoad);
                        writer.WriteLine("\n");
                    }
                }
                writer.WriteLine("\n");
                writer.WriteLine("Customer Info:");

                int cnt = 0;
                writer.WriteLine("CustomerID:" + -1);
                writer.WriteLine("Stops(RideOn, RideOff): (" + -1 + "," + -1 + ")");
                writer.WriteLine("DesiredTimeWindow(RideOn, RideOFf): (" + -1 + "," + -1 + ")");
                writer.WriteLine("RealTimeWindow(RideOn, RideOFf): (" + -1 + "," + -1 + ")");
                writer.WriteLine("WaitTime: " + -1);
                writer.WriteLine("DelayTime: " + -1);
                foreach (Customer customer in Customers)
                {
                    writer.WriteLine("CustomerID:" + cnt);
                    writer.WriteLine("Stops(RideOn, RideOff): (" + customer.PickupDelivery[0].Id + "," + customer.PickupDelivery[1].Id + ")");
                    writer.WriteLine("DesiredTimeWindow(RideOn, RideOFf): (" + customer.DesiredTimeWindow[0] + "," +customer.DesiredTimeWindow[1] + ")");
                    writer.WriteLine("RealTimeWindow(RideOn, RideOFf): (" + customer.RealTimeWindow[0] + "," + customer.RealTimeWindow[1] + ")");
                    writer.WriteLine("WaitTime: " + customer.WaitTime);
                    writer.WriteLine("DelayTime: " + customer.DelayTime);
                    cnt++;
                    writer.WriteLine("");
                }
                writer.WriteLine("CustomerID:" + -2);
                writer.WriteLine("Stops(RideOn, RideOff): (" + -2 + "," + -2 + ")");
                writer.WriteLine("DesiredTimeWindow(RideOn, RideOFf): (" + -2 + "," + -2 + ")");
                writer.WriteLine("RealTimeWindow(RideOn, RideOFf): (" + -2 + "," + -2 + ")");
                writer.WriteLine("WaitTime: " + -2);
                writer.WriteLine("DelayTime: " + -2);
            }
            using (StreamWriter writer = new StreamWriter(path2, append: false)) // append: true で追記
            {
                writer.WriteLine("Indicators:");
                for (int index = 0; index < Indicators.Count; index++)
                {
                    writer.Write(Indicators[index]);
                    if (index != Indicators.Count - 1)
                    {
                        writer.Write(",");
                    }
                }
                writer.WriteLine();

                writer.WriteLine("Vehicle Route Info:");

                for (int vehicleId = 0; vehicleId < VehicleRoutes.Count; vehicleId++)
                {
                    writer.WriteLine("VehicleID:" + vehicleId);
                    List<RouteStep> routeSteps = VehicleRoutes[vehicleId];

                    writer.WriteLine("stepId,stopId,requestID, pickupOrDelivery,ArrivalTime,DepatureTime, CumulativeLoad");
                    writer.WriteLine("-1" + "," + dataModel.IndexManager.Stops[dataModel.Starts[vehicleId]].Id + "," + -1 + "," + -1 + "," +  0 + "," + 0 + "," + 0);
                    for (int stepId = 0; stepId < routeSteps.Count; stepId++)
                    {
                        RouteStep currentStep = routeSteps[stepId];
                        writer.WriteLine("" +stepId + "," + Customers[currentStep.RequestID].PickupDelivery[currentStep.PickupOrDelivery].Id + "," + currentStep.RequestID + "," + currentStep.PickupOrDelivery + "," +  currentStep.ArrivalTime + "," + currentStep.DepatureTime + "," + currentStep.CumulativeLoad);
                    }
                    writer.WriteLine("-2" + "," + dataModel.IndexManager.Stops[dataModel.Ends[vehicleId]].Id + "," + -2 + "," + -1 + "," + LastStep[vehicleId].ArrivalTime + "," + LastStep[vehicleId].DepatureTime + "," + LastStep[vehicleId].CumulativeLoad);
                }
                writer.WriteLine("Customer Info:");

                int cnt = 0;
                writer.WriteLine("CustomerID, RideOnStop,RideOffStop,DesiredRideOnTime,DesiredRideOffTime,RealRideOnTime,RealRideOffTime,WaitTime,DelayTime");

                writer.WriteLine(-1 + "," + -1 + "," + -1 + "," + -1 + "," + -1 + "," + -1 + "," + -1 + "," + -1 + "," + -1);
                foreach (Customer customer in Customers)
                {
                    writer.WriteLine(cnt + "," + customer.PickupDelivery[0].Id + "," + customer.PickupDelivery[1].Id + "," + customer.DesiredTimeWindow[0] + "," + customer.DesiredTimeWindow[1] + "," + customer.RealTimeWindow[0] + "," + customer.RealTimeWindow[1] + "," + customer.WaitTime + "," +customer.DelayTime);
                    cnt++;
                }
                writer.WriteLine(-2 + "," + -2 + "," + -2 + "," + -2 + "," + -2 + "," + -2 + "," + -2 + "," + -2 + "," + -2);
            }
        }

        public void AppendCSVSolutionData()
        {
            if (First == 1)
            {
                First = 0;
                WirteFirstLine();
            }
            using (StreamWriter writer = new StreamWriter(path, append: true)) // append: true で追記
            {
                string tmp = "";

                // 解の付属情報
                tmp += Id;
                tmp += "," + myEvalCnt;
                tmp += "," + geneCnt;
                
                for (int i = 0; i < ObjectiveFunctions.Count; i++)
                {
                    tmp += "," + ObjectiveFunctions[i];
                }
                for (int i = 0; i < gene.Length; i++)
                {
                    tmp += "," + gene[i];
                }
                for (int i = 0; i < Indicators.Count; i++)
                {
                    tmp += "," + Indicators[i];
                }
                writer.WriteLine(tmp);
            }
        }
    }

    
}
