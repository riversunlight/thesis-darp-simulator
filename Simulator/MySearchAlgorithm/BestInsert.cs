using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Google.OrTools.ConstraintSolver;
using Google.OrTools.Sat;
using Simulator.Objects.Data_Objects.Routing;
using Simulator.Objects.Simulation;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.IO;
using System.Diagnostics;
using Simulator.Objects.Data_Objects.Simulation_Objects;

// 基底クラス
// シミュレータは絶対にInitの直後でTryGetSolutionを使うようにすること!!!
// TryGetSolutionは1回だけの呼び出しが望ましい
namespace Simulator.MySearchAlgorithm
{

    public class BestInsert
    {
        public RoutingModel RoutingModel;
        public RoutingIndexManager Manager;
        public RoutingDataModel DataModel;
        public int generationCnt;
        public int evolutionCnt;
        public Random randomCreater;
        public int VehicleNum;
        public int NodeNum;
        public int CustomerNum;
        public int mutatePriority;
        public int limitSeconds;

        public int FirstWrited;
        public int SolutionId;
        public Stopwatch sw;

        // Vianaを参考にしよう
        public BestInsert(RoutingModel _routingModel, RoutingIndexManager _manager, RoutingDataModel _DataModel)
        {
            RoutingModel = _routingModel;
            Manager = _manager;
            DataModel = _DataModel;
            randomCreater = new Random();
            FirstWrited = 0;
            SolutionId = 0;
            limitSeconds =60;
            MyAssignment dummy = new MyAssignment(1);
            dummy.resetEvalCnt();

            VehicleNum = Manager.GetNumberOfVehicles();
            NodeNum = Manager.GetNumberOfNodes();
            CustomerNum = DataModel.PickupsDeliveries.Length;
        }

        public int GiveId()
        {
            return SolutionId++;
        }



        /*
        public virtual void OutputSolutionData(string path, int gene_cnt)
        {
            if (FirstWrited == 0)
            {
                using (StreamWriter writer = new StreamWriter(path, append: false))
                {
                    string tmp = "ID, Evaluation,Generation";
                    for (int i = 0; i < population[0].ObjectiveFunctions.Count; i++)
                    {
                        tmp += ",Object" + i;
                    }
                    for (int i = 0; i < population[0].gene.Length; i++)
                    {
                        tmp += ",Gene" + i;
                    }

                    writer.WriteLine(tmp);
                }
                FirstWrited = 1;
            }
            for (int i = 0; i < population_size; i++)
            {
                population[i].AppendCSVSolutionData(path, -1, gene_cnt);
            }
        }
        */


        public int ConvertIndex2LStopId(int vehicle_count, int index)
        {
            return 2 * vehicle_count + index;
        }

        public virtual void Solve()
        {
            int[] numbers = new int[CustomerNum];
            for (int i = 0; i < CustomerNum; i++)
            {
                numbers[i] = i;
            }
            // customer * 2の順列のシャッフル
            // 0, 1のあれは修正する。
            int[] solutionP = numbers.OrderBy(x => randomCreater.Next()).ToArray();
            MyAssignment firstSolution = new MyAssignment(CustomerNum);
            List<int> childGeneList  = new List<int>();

            // 最適挿入(単純な距離基準)
            for (int i = 0; i < numbers.Length; i++)
            {
                int customerId = solutionP[i];
                long bestScore = 100000000;
                int bestPickPoint = -1, bestDeliveryPoint = -1;
                int customerPickupStopId = ConvertIndex2LStopId(VehicleNum, customerId * 2);
                int customerDeliveryStopId = ConvertIndex2LStopId(VehicleNum, customerId * 2 + 1);
                for (int pickupPoint = 0; pickupPoint <= childGeneList.Count; pickupPoint++)
                {
                    int previousStopId = DataModel.Starts[0]; // 車両1台のみの前提
                    int nextStopId = DataModel.Ends[0];
                    if (pickupPoint != 0)
                    {
                        previousStopId = ConvertIndex2LStopId(VehicleNum, childGeneList[pickupPoint - 1]);
                    }
                    if (pickupPoint != childGeneList.Count)
                    {
                        nextStopId = ConvertIndex2LStopId(VehicleNum, childGeneList[pickupPoint]);
                    }


                    long dist = DataModel.TravelTimes[previousStopId, customerPickupStopId] + DataModel.TravelTimes[customerPickupStopId, nextStopId] - DataModel.TravelTimes[previousStopId, nextStopId];

                    if (dist < bestScore)
                    {
                        bestScore = dist;
                        bestPickPoint = pickupPoint;
                    }
                }
                if (bestPickPoint == -1)
                {
                    int a;
                    a = 10;
                }
                childGeneList.Insert(bestPickPoint, customerId * 2);

                bestScore = 10000000000;
                bestDeliveryPoint = bestPickPoint + 1;

                for (int deliveryPoint = bestPickPoint + 1; deliveryPoint <= childGeneList.Count; deliveryPoint++)
                {
                    int previousStopId = DataModel.Starts[0]; // 車両1台のみの前提
                    int nextStopId = DataModel.Ends[0];
                    if (deliveryPoint != 0)
                    {
                        previousStopId = ConvertIndex2LStopId(VehicleNum, childGeneList[deliveryPoint - 1]);
                    }
                    if (deliveryPoint != childGeneList.Count)
                    {
                        nextStopId = ConvertIndex2LStopId(VehicleNum, childGeneList[deliveryPoint]);
                    }
                    long dist = DataModel.TravelTimes[previousStopId, customerDeliveryStopId] + DataModel.TravelTimes[customerDeliveryStopId, nextStopId] - DataModel.TravelTimes[previousStopId, nextStopId];

                    if (dist < bestScore)
                    {
                        bestScore = dist;
                        bestDeliveryPoint = deliveryPoint;
                    }
                }

                if (bestDeliveryPoint == -1)
                {
                    int a;
                    a = 10;
                }
                childGeneList.Insert(bestDeliveryPoint, customerId * 2 + 1);
            }


            for (int j = 0; j < childGeneList.Count; j++)
            {
                firstSolution.gene[0].Add(childGeneList[j]);
            }
            firstSolution.Simulate(DataModel);
            firstSolution.SetId(GiveId());
        }

        public virtual int StoppingCondition()
        {

            if (sw.Elapsed.TotalSeconds > limitSeconds)
            {
                return 1;
            }
            return 0;
            //if (generationCnt == 1000) return 1;
            //return 0;
        }


        public virtual void TryGetSolution(string path, string objCase, string _crossOver)
        {
            sw = Stopwatch.StartNew();
            MyAssignment dummy = new MyAssignment(0);
            dummy.setPath(path);
            dummy.setObjCase(objCase);
            dummy.setGeneCnt(0);


            while (StoppingCondition() == 0)
            {
                Solve();
                generationCnt++;
                dummy.setGeneCnt(generationCnt);
            }

        }
    }
}
