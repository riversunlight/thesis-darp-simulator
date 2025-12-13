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
using System.Text.RegularExpressions;
using MathNet.Numerics.Distributions;
using System.Security.Cryptography;

// 基底クラス
// シミュレータは絶対にInitの直後でTryGetSolutionを使うようにすること!!!
// TryGetSolutionは1回だけの呼び出しが望ましい
namespace Simulator.MySearchAlgorithm
{

    public class BaseGA
    {
        public RoutingModel RoutingModel;
        public RoutingIndexManager Manager;
        public RoutingDataModel DataModel;
        public int population_size;
        public int offspring_size;
        public string crossOverKind;
        public string mutateKind;
        public string initialKind;


        public List<MyAssignment> population;
        public List<MyAssignment> offspring;
        public int generationCnt;
        public int evolutionCnt;
        public Random randomCreater;
        public int VehicleNum;
        public int NodeNum;
        public int CustomerNum;
        public int mutatePriority;
        public int limitSeconds;
        public long Capacity;
        public long MaxDelay;
        public long MaxWait;

        public int FirstWrited;
        public int SolutionId;
        public Stopwatch sw;

        // Vianaを参考にしよう
        public BaseGA(RoutingModel _routingModel, RoutingIndexManager _manager, RoutingDataModel _DataModel)
        {
            RoutingModel = _routingModel;
            Manager = _manager;
            DataModel = _DataModel;
            population_size = 20;
            offspring_size = 20;
            mutatePriority = 10;
            randomCreater = new Random();
            FirstWrited = 0;
            SolutionId = 0;
            limitSeconds = 300;
            MyAssignment dummy = new MyAssignment(1);
            dummy.resetEvalCnt();

            Capacity = long.MaxValue;
            MaxDelay = long.MaxValue;
            MaxWait = long.MaxValue;

            VehicleNum = Manager.GetNumberOfVehicles();
            NodeNum = Manager.GetNumberOfNodes();
            CustomerNum = DataModel.PickupsDeliveries.Length;
        }

        public int GiveId()
        {
            return SolutionId++;
        }

        public void CrossOverOnePoint() // 1点交叉、O(n)
        {
            offspring = new List<MyAssignment>();

            for (int i = 0; i < offspring_size / 2; i++)
            {
                int p1 = -1, p2 = -1;
                SelectParent(ref p1, ref p2);
                int[] p1_gene = new int[CustomerNum * 2];
                int[] p2_gene = new int[CustomerNum * 2];

                for (int j = 0; j < CustomerNum * 2; j++)
                {
                    p1_gene[j] = population[p1].gene[0][j];
                    p2_gene[j] = population[p2].gene[0][j];
                }
                for (int j = 0; j < 2; j++)
                {
                    int[] parentA = (j == 0) ? p1_gene : p2_gene;
                    int[] parentB = (j == 0) ? p2_gene : p1_gene;

                    List<int> childGeneList = new List<int>();
                    int cutPoint = randomCreater.Next(1, parentA.Length);
                    int[] already_exist = new int[p1_gene.Length];

                    for (int k = 0; k < cutPoint; k++)
                    {
                        already_exist[parentA[k]] = 1;
                        childGeneList.Add(parentA[k]);
                    }
                    for (int k = 0; k < p1_gene.Length; k++)
                    {
                        if (already_exist[parentB[k]] == 0)
                            childGeneList.Add(parentB[k]);

                    }

                    MyAssignment offspringSolution = new MyAssignment(CustomerNum);
                    for (int k = 0; k < 2 * CustomerNum; k++)
                    {
                        offspringSolution.gene[0][k] = childGeneList[k];
                    }
                    Mutate(ref offspringSolution);
                    offspringSolution.SetId(GiveId());
                    offspring.Add(offspringSolution);
                }

            }

        }

        public void CrossOverPPX()
        {
            offspring = new List<MyAssignment>();

            for (int i = 0; i < offspring_size / 2; i++)
            {
                int p1 = -1, p2 = -1;
                SelectParent(ref p1, ref p2);

                int[] p1_gene = new int[CustomerNum * 2];
                int[] p2_gene = new int[CustomerNum * 2];

                for (int j = 0; j < CustomerNum * 2; j++)
                {
                    p1_gene[j] = population[p1].gene[0][j];
                    p2_gene[j] = population[p2].gene[0][j];
                }


                for (int j = 0; j < 2; j++)
                {

                    int[] parentA = (j == 0) ? p1_gene : p2_gene;
                    int[] parentB = (j == 0) ? p2_gene : p1_gene;
                    int n = parentA.Length;
                    int[] child = new int[n];
                    bool[] used = new bool[n + 1]; // ID基準で使用チェック
                    int idx = 0;

                    // ① 親を結合して並び順を混ぜる
                    var merged = new List<int>();
                    merged.AddRange(parentA);
                    merged.AddRange(parentB);

                    // ② 各遺伝子にランダムキーを付ける
                    var keyList = merged.Select(g => new { gene = g, key = randomCreater.NextDouble() }).ToList();
                    var sorted = keyList.OrderBy(x => x.key).Select(x => x.gene).ToList();

                    // ③ 順序制約を守りつつ子を構築
                    foreach (int g in sorted)
                    {
                        int id = g / 2; // ペアID
                        bool isPickup = (g % 2 == 0); // 例: 偶数ならpickup

                        if (used[g]) continue;

                        if (isPickup)
                        {
                            child[idx++] = g;
                            used[g] = true;
                        }
                        else
                        {
                            int pickup = g - 1; // pickupは1つ前
                            if (!used[pickup])
                            {
                                child[idx++] = pickup;
                                used[pickup] = true;
                            }
                            child[idx++] = g;
                            used[g] = true;
                        }

                        if (idx >= n) break;
                    }

                    MyAssignment offspringSolution = new MyAssignment(CustomerNum);
                    for (int k = 0; k < CustomerNum * 2; k++)
                    {
                        offspringSolution.gene[0][k] = child[k];
                    }
                    Mutate(ref offspringSolution);
                    offspringSolution.Simulate(DataModel);
                    offspringSolution.SetId(GiveId());
                    offspring.Add(offspringSolution);

                }
            }
        }


        // 親は毎回選べるように
        public void CrossOverViana() // Viana, O(n^3)
        {
            offspring = new List<MyAssignment>();
            for (int i = 0; i < offspring_size / 2; i++)
            {
                int p1 = -1, p2 = -1;
                SelectParent(ref p1, ref p2);
                int[] p1_gene = new int[CustomerNum * 2];
                int[] p2_gene = new int[CustomerNum * 2];

                for (int j = 0; j < CustomerNum * 2; j++)
                {
                    p1_gene[j] = population[p1].gene[0][j];
                    p2_gene[j] = population[p2].gene[0][j];
                }

                for (int j = 0; j < 2; j++)
                {
                    int[] parentA = (j == 0) ? p1_gene : p2_gene;
                    int[] parentB = (j == 0) ? p2_gene : p1_gene;

                    List<int> childGeneList = new List<int>();
                    int cutPoint = randomCreater.Next(1, parentA.Length);

                    for (int k = 0; k < p1_gene.Length; k++)
                    {
                        if (k < cutPoint)
                        {
                            childGeneList.Add(parentA[k]);
                        }
                        else
                        {
                            childGeneList.Add(parentB[k]);
                        }
                    }
                    HashSet<int> insertedPickupRequestIDs = new HashSet<int>();
                    HashSet<int> insertedDeliveryRequestIDs = new HashSet<int>();

                    List<int> toBeRemoved = new List<int>();
                    for (int k = 0; k < childGeneList.Count; k++)
                    {
                        int customerId = childGeneList[k] / 2;
                        int pickupDelivery = childGeneList[k] % 2;
                        if (pickupDelivery == 0)
                        {
                            if (insertedPickupRequestIDs.Contains(customerId))
                            {
                                toBeRemoved.Add(k);
                                continue;
                            }
                            insertedPickupRequestIDs.Add(customerId);
                        }
                        else
                        {
                            if (!insertedPickupRequestIDs.Contains(customerId))
                            {
                                toBeRemoved.Add(k);
                                continue;
                            }
                            if (insertedDeliveryRequestIDs.Contains(customerId))
                            {
                                toBeRemoved.Add(k);
                                continue;
                            }
                            insertedDeliveryRequestIDs.Add(customerId);
                        }
                    }
                    for (int k = toBeRemoved.Count - 1; k >= 0; k--)
                    {
                        childGeneList.RemoveAt(toBeRemoved[k]);
                    }
                    foreach (int customerId in insertedPickupRequestIDs)
                    {
                        if (!insertedDeliveryRequestIDs.Contains(customerId))
                        { // PickUpしか入っていないやつ
                            childGeneList.Remove(customerId * 2);

                        }
                    }

                    // 最適挿入(1個目の目的関数を使用)
                    for (int customerId = 0; customerId < p1_gene.Length / 2; customerId++)
                    {
                        if (!insertedDeliveryRequestIDs.Contains(customerId))
                        {
                            long bestScore = long.MaxValue;
                            List<int> bestChildGene = null;
                            for (int pickupPoint = 0; pickupPoint < childGeneList.Count; pickupPoint++)
                            {
                                childGeneList.Insert(pickupPoint, customerId * 2);
                                for (int deliveryPoint = pickupPoint + 1; deliveryPoint < childGeneList.Count; deliveryPoint++)
                                {
                                    childGeneList.Insert(deliveryPoint, customerId * 2 + 1);
                                    MyAssignment tempSolution = new MyAssignment(CustomerNum);
                                    tempSolution.gene.Add(new List<int>());
                                    for (int k = 0; k < p1_gene.Length; k++)
                                    {
                                        if (k < childGeneList.Count)
                                        {
                                            tempSolution.gene[0].Add(childGeneList[k]);
                                        }
                                        else
                                        {
                                            tempSolution.gene[0].Add(-1);
                                        }
                                    }
                                    tempSolution.Simulate(DataModel);
                                    if (tempSolution.ObjectiveFunctionRouteLength() < bestScore)
                                    {
                                        bestScore = tempSolution.ObjectiveFunctionRouteLength();
                                        bestChildGene = new List<int>(childGeneList);
                                    }
                                    childGeneList.RemoveAt(deliveryPoint);
                                }
                                childGeneList.RemoveAt(pickupPoint);

                            }
                            childGeneList = new List<int>(bestChildGene);
                        }
                    }

                    MyAssignment offspringSolution = new MyAssignment(CustomerNum);
                    for (int k = 0; k < 2 * CustomerNum; k++)
                    {
                        offspringSolution.gene[0][k] = childGeneList[k];
                    }
                    Mutate(ref offspringSolution);
                    offspringSolution.Simulate(DataModel);
                    offspringSolution.SetId(GiveId());
                    offspring.Add(offspringSolution);
                }

            }
        }

        public int ConvertIndex2LStopId(int vehicle_count, int index)
        {
            return 2 * vehicle_count + index;
        }
        public void CrossOverVianaFast() // Viana: 最適挿入のところを高速化 1台専用
        {
            offspring = new List<MyAssignment>();
            for (int i = 0; i < offspring_size / 2; i++)
            {
                int p1 = -1, p2 = -1;
                SelectParent(ref p1, ref p2);
                
                int[] p1_gene = new int[CustomerNum * 2];
                int[] p2_gene = new int[CustomerNum * 2];

                for (int j = 0; j < CustomerNum * 2; j++)
                {
                    p1_gene[j] = population[p1].gene[0][j];
                    p2_gene[j] = population[p2].gene[0][j];

                }

                for (int j = 0; j < 2; j++)
                {
                    int[] parentA = (j == 0) ? p1_gene : p2_gene;
                    int[] parentB = (j == 0) ? p2_gene : p1_gene;

                    List<int> childGeneList = new List<int>();
                    int cutPoint = randomCreater.Next(1, parentA.Length);

                    for (int k = 0; k < p1_gene.Length; k++)
                    {
                        if (k < cutPoint)
                        {
                            childGeneList.Add(parentA[k]);
                        }
                        else
                        {
                            childGeneList.Add(parentB[k]);
                        }
                    }
                    HashSet<int> insertedPickupRequestIDs = new HashSet<int>();
                    HashSet<int> insertedDeliveryRequestIDs = new HashSet<int>();

                    List<int> toBeRemoved = new List<int>();
                    for (int k = 0; k < childGeneList.Count; k++)
                    {
                        int customerId = childGeneList[k] / 2;
                        int pickupDelivery = childGeneList[k] % 2;
                        if (pickupDelivery == 0)
                        {
                            if (insertedPickupRequestIDs.Contains(customerId))
                            {
                                toBeRemoved.Add(k);
                                continue;
                            }
                            insertedPickupRequestIDs.Add(customerId);
                        }
                        else
                        {
                            if (!insertedPickupRequestIDs.Contains(customerId))
                            {
                                toBeRemoved.Add(k);
                                continue;
                            }
                            if (insertedDeliveryRequestIDs.Contains(customerId))
                            {
                                toBeRemoved.Add(k);
                                continue;
                            }
                            insertedDeliveryRequestIDs.Add(customerId);
                        }
                    }
                    for (int k = toBeRemoved.Count - 1; k >= 0; k--)
                    {
                        childGeneList.RemoveAt(toBeRemoved[k]);
                    }
                    foreach (int customerId in insertedPickupRequestIDs)
                    {
                        if (!insertedDeliveryRequestIDs.Contains(customerId))
                        { // PickUpしか入っていないやつ
                            childGeneList.Remove(customerId * 2);

                        }
                    }

                    // 最適挿入(単純な距離基準)
                    for (int customerId = 0; customerId < p1_gene.Length / 2; customerId++)
                    {
                        if (!insertedDeliveryRequestIDs.Contains(customerId))
                        {
                            long bestScore = long.MaxValue;
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

                            bestScore = long.MaxValue;

                            for (int deliveryPoint = bestPickPoint + 1; deliveryPoint <= childGeneList.Count; deliveryPoint++) {
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
                    }

                    MyAssignment offspringSolution = new MyAssignment(CustomerNum);
                    offspringSolution.gene.Add(new List<int>());
                    for (int k = 0; k < 2 * CustomerNum; k++)
                    {
                        offspringSolution.gene[0].Add(childGeneList[k]);
                    }
                    Mutate(ref offspringSolution);
                    offspringSolution.Simulate(DataModel);
                    offspringSolution.SetId(GiveId());
                    offspring.Add(offspringSolution);
                }

            }
        }

        private void InsertRequestBest(ref List<List<int>> gene, int cid)
        {
            int pickup = cid * 2;
            int delivery = cid * 2 + 1;

            long bestCost = long.MaxValue;
            int bestRoute = -1;
            int bestP = -1;
            int bestD = -1;

            for (int vehicleId = 0; vehicleId < gene.Count; vehicleId++)
            {
                var route = gene[vehicleId];

                for (int pickupPoint = 0; pickupPoint <= route.Count; pickupPoint++)
                {
                    for (int deliveryPoint = pickupPoint + 1; deliveryPoint <= route.Count + 1; deliveryPoint++)
                    {
                        // 試し挿入
                        route.Insert(pickupPoint, pickup);
                        route.Insert(deliveryPoint, delivery);

                        long dist = CheckRouteFeasible(DataModel, route);
                        if (dist >= 0)
                        {

                            if (dist < bestCost)
                            {
                                bestCost = dist;
                                bestRoute = vehicleId;
                                bestP = pickupPoint;
                                bestD = deliveryPoint;
                            }
                        }

                        // 戻す
                        route.RemoveAt(deliveryPoint);
                        route.RemoveAt(pickupPoint);
                    }
                }
            }

            if (bestRoute != -1)
            {
                gene[bestRoute].Insert(bestP, pickup);
                gene[bestRoute].Insert(bestD, delivery);
                return;
            }
            gene.Add(new List<int> { pickup, delivery });
        }

        public void CrossOverConst() // 制約条件を考慮
        {

            offspring = new List<MyAssignment>();
            for (int i = 0; i < offspring_size / 2; i++)
            {
                int p1 = -1, p2 = -1;
                SelectParent(ref p1, ref p2);

                int route1Length = population[p1].gene.Count;
                int route2Length = population[p2].gene.Count;

                for (int j = 0; j < 2; j++)
                {
                    int firstParent = (j == 0) ? p1 : p2;
                    int secondParent = (j == 0) ? p2 : p1;

                    List<List<int>> childGeneList = new List<List<int>>();
                    int cutPoint = randomCreater.Next(0, (route1Length < route2Length) ? route1Length : route2Length);

                    HashSet<int> insertedRequestIDs = new HashSet<int>();
                    for (int k = 0; k < cutPoint; k++)
                    {
                        childGeneList.Add(new List<int>(population[firstParent].gene[k]));

                    }
                    for (int k = 0; k < childGeneList.Count; k++)
                    {
                        for (int k2 = 0; k2 < childGeneList[k].Count; k2++)
                        {
                            insertedRequestIDs.Add(childGeneList[k][k2] / 2);
                        }
                    }
                    for (int k = cutPoint; k < population[secondParent].gene.Count; k++)
                    {
                        bool first = true;
                        for (int k2 = 0; k2 < population[secondParent].gene[k].Count; k2++)
                        {
                            if (insertedRequestIDs.Contains(population[secondParent].gene[k][k2] / 2)) continue;
                            if (first)
                            {
                                childGeneList.Add(new List<int>());
                                first = false;
                            }
                            childGeneList[childGeneList.Count - 1].Add(population[secondParent].gene[k][k2]);
                        }
                    }
                    for (int k = 0; k < childGeneList.Count; k++)
                    {
                        for (int k2 = 0; k2 < childGeneList[k].Count; k2++)
                        {
                            insertedRequestIDs.Add(childGeneList[k][k2] / 2);
                        }
                    }

                    for (int customerId = 0; customerId < CustomerNum; customerId++)
                    {
                        if (!insertedRequestIDs.Contains(customerId))
                        {
                            InsertRequestBest(ref childGeneList, customerId);
                        }
                    }



                    MyAssignment offspringSolution = new MyAssignment(CustomerNum);
                    offspringSolution.gene = childGeneList.Select(innerList => new List<int>(innerList)).ToList();
                    Mutate(ref offspringSolution);
                    offspringSolution.Simulate(DataModel);
                    offspringSolution.SetId(GiveId());
                    offspring.Add(offspringSolution);
                }

            }

        }

        public void CrossOverVianaFastMulti() // Viana: 最適挿入のところを高速化
        {
            
            offspring = new List<MyAssignment>();
            for (int i = 0; i < offspring_size / 2; i++)
            {
                int p1 = -1, p2 = -1;
                SelectParent(ref p1, ref p2);

                int route1Length = population[p1].gene.Count;
                int route2Length = population[p2].gene.Count;

                for (int j = 0; j < 2; j++)
                {
                    int firstParent = (j == 0)? p1: p2;
                    int secondParent = (j == 0) ? p2 : p1;

                    List<List<int>> childGeneList = new List<List<int>>();
                    int cutPoint = randomCreater.Next(0, (route1Length<route2Length)?route1Length:route2Length);

                    HashSet<int> insertedRequestIDs = new HashSet<int>();
                    for (int k = 0; k < cutPoint; k++)
                    {
                        childGeneList.Add(new List<int>(population[firstParent].gene[k]));
                        
                    }
                    for (int k = 0; k < childGeneList.Count; k++)
                    {
                        for (int k2 = 0; k2 <  childGeneList[k].Count; k2++)
                        {
                            insertedRequestIDs.Add(childGeneList[k][k2] / 2);
                        }
                    }
                    for (int k = cutPoint; k < population[secondParent].gene.Count; k++)
                    {
                        bool first = true;
                        for (int k2 = 0; k2 < population[secondParent].gene[k].Count; k2++)
                        {
                            if (insertedRequestIDs.Contains(population[secondParent].gene[k][k2] / 2)) continue;
                            if (first)
                            {
                                childGeneList.Add(new List<int>());
                                first = false;
                            }
                            childGeneList[childGeneList.Count - 1].Add(population[secondParent].gene[k][k2]);
                        }
                    }
                    for (int k = 0; k < childGeneList.Count; k++)
                    {
                        for (int k2 = 0; k2 < childGeneList[k].Count; k2++)
                        {
                            insertedRequestIDs.Add(childGeneList[k][k2] / 2);
                        }
                    }



                    // 最適挿入(単純な距離基準)
                    for (int customerId = 0; customerId < CustomerNum; customerId++)
                    {
                        if (!insertedRequestIDs.Contains(customerId))
                        {
                            long bestScore = long.MaxValue;
                            int bestPickPoint = -1, bestDeliveryPoint = -1;
                            int bestPickVehicle = -1;
                            int customerPickupStopId = ConvertIndex2LStopId(VehicleNum, customerId * 2);
                            int customerDeliveryStopId = ConvertIndex2LStopId(VehicleNum, customerId * 2 + 1);
                            for (int vehicleId = 0; vehicleId < childGeneList.Count; vehicleId++)
                            {
                                for (int pickupPoint = 0; pickupPoint <= childGeneList[vehicleId].Count; pickupPoint++)
                                {
                                    int previousStopId = DataModel.Starts[0]; // デポ1箇所のみ
                                    int nextStopId = DataModel.Ends[0];
                                    if (pickupPoint != 0)
                                    {
                                        previousStopId = ConvertIndex2LStopId(VehicleNum, childGeneList[vehicleId][pickupPoint - 1]);
                                    }
                                    if (pickupPoint != childGeneList[vehicleId].Count)
                                    {
                                        nextStopId = ConvertIndex2LStopId(VehicleNum, childGeneList[vehicleId][pickupPoint]);
                                    }


                                    long dist = DataModel.TravelTimes[previousStopId, customerPickupStopId] + DataModel.TravelTimes[customerPickupStopId, nextStopId] - DataModel.TravelTimes[previousStopId, nextStopId];

                                    if (dist < bestScore)
                                    {
                                        bestScore = dist;
                                        bestPickPoint = pickupPoint;
                                        bestPickVehicle = vehicleId;
                                    }
                                }
                            }
                            if (bestPickPoint == -1)
                            {
                                int a;
                                a = 10;
                            }
                            childGeneList[bestPickVehicle].Insert(bestPickPoint, customerId * 2);

                            bestScore = long.MaxValue;

                            for (int deliveryPoint = bestPickPoint + 1; deliveryPoint <= childGeneList[bestPickVehicle].Count; deliveryPoint++)
                            {
                                int previousStopId = DataModel.Starts[0]; // 車両1台のみの前提
                                int nextStopId = DataModel.Ends[0];
                                if (deliveryPoint != 0)
                                {
                                    previousStopId = ConvertIndex2LStopId(VehicleNum, childGeneList[bestPickVehicle][deliveryPoint - 1]);
                                }
                                if (deliveryPoint != childGeneList[bestPickVehicle].Count)
                                {
                                    nextStopId = ConvertIndex2LStopId(VehicleNum, childGeneList[bestPickVehicle][deliveryPoint]);
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
                            childGeneList[bestPickVehicle].Insert(bestDeliveryPoint, customerId * 2 + 1);
                        }
                    }

                    MyAssignment offspringSolution = new MyAssignment(CustomerNum);
                    offspringSolution.gene = childGeneList.Select(innerList => new List<int>(innerList)).ToList(); ;
                    Mutate(ref offspringSolution);
                    offspringSolution.Simulate(DataModel);
                    offspringSolution.SetId(GiveId());
                    offspring.Add(offspringSolution);
                }

            }
            
        }

        public void BestInsert(ref List<List<int>> childGeneList, int customerId)
        {
            long bestScore = long.MaxValue;
            int bestPickPoint = -1, bestDeliveryPoint = -1;
            int bestPickVehicle = -1;
            int customerPickupStopId = ConvertIndex2LStopId(VehicleNum, customerId * 2);
            int customerDeliveryStopId = ConvertIndex2LStopId(VehicleNum, customerId * 2 + 1);
            for (int vehicleId = 0; vehicleId < childGeneList.Count; vehicleId++)
            {
                for (int pickupPoint = 0; pickupPoint <= childGeneList[vehicleId].Count; pickupPoint++)
                {
                    int previousStopId = DataModel.Starts[0]; // デポ1箇所のみ
                    int nextStopId = DataModel.Ends[0];
                    if (pickupPoint != 0)
                    {
                        previousStopId = ConvertIndex2LStopId(VehicleNum, childGeneList[vehicleId][pickupPoint - 1]);
                    }
                    if (pickupPoint != childGeneList[vehicleId].Count)
                    {
                        nextStopId = ConvertIndex2LStopId(VehicleNum, childGeneList[vehicleId][pickupPoint]);
                    }


                    long dist = DataModel.TravelTimes[previousStopId, customerPickupStopId] + DataModel.TravelTimes[customerPickupStopId, nextStopId] - DataModel.TravelTimes[previousStopId, nextStopId];

                    if (dist < bestScore)
                    {
                        bestScore = dist;
                        bestPickPoint = pickupPoint;
                        bestPickVehicle = vehicleId;
                    }
                }
            }
            if (bestPickPoint == -1)
            {
                int a;
                a = 10;
            }
            childGeneList[bestPickVehicle].Insert(bestPickPoint, customerId * 2);

            bestScore = long.MaxValue;

            for (int deliveryPoint = bestPickPoint + 1; deliveryPoint <= childGeneList[bestPickVehicle].Count; deliveryPoint++)
            {
                int previousStopId = DataModel.Starts[0]; // 車両1台のみの前提
                int nextStopId = DataModel.Ends[0];
                if (deliveryPoint != 0)
                {
                    previousStopId = ConvertIndex2LStopId(VehicleNum, childGeneList[bestPickVehicle][deliveryPoint - 1]);
                }
                if (deliveryPoint != childGeneList[bestPickVehicle].Count)
                {
                    nextStopId = ConvertIndex2LStopId(VehicleNum, childGeneList[bestPickVehicle][deliveryPoint]);
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
            childGeneList[bestPickVehicle].Insert(bestDeliveryPoint, customerId * 2 + 1);
        }

        public virtual void CrossOver()
        {
            switch (crossOverKind)
            {
                case "VianaFast":
                    CrossOverVianaFast();
                    break;
                case "Viana":
                    CrossOverViana();
                    break;
                case "Multi":
                    CrossOverVianaFastMulti();
                    break;
                case "CONST":
                    CrossOverConst();
                    break;
                default:
                    CrossOverViana();
                    break;
            }
        }
        public virtual void Mutate(ref MyAssignment solution)
        {
            switch (mutateKind)
            {
                case "Single":
                    MutateSingle(ref solution);
                    break;
                case "Multi":
                    MutateMulti(ref solution);
                    break;
                case "CONST":
                    MutateMultiConst(ref solution);
                    break;
                default:
                    MutateSingle(ref solution);
                    break;
            }
        }

        public virtual void InitialPopulation()
        {
            switch (initialKind)
            {
                case "Single":
                    InitialPopulationSingle();
                    break;
                case "Multi":
                    InitialPopulationMulti();
                    break;
                case "Const":
                    InitialPopulationConstGuided();
                    break;
                default:
                    InitialPopulationSingle();
                    break;
            }
        }
        public virtual void SetUpConstraint(long _capa, long _max_delay, long _max_wait)
        {
            Capacity = _capa;
            MaxDelay = _max_delay;
            MaxWait = _max_wait;
        }

        // 一様にするか
        public virtual void SelectParent(ref int p1, ref int p2)
        {
            p1 = randomCreater.Next(population_size);
            p2 = randomCreater.Next(population_size - 1);
            if (p1 == p2) p2 = population_size - 1;
        }

        // 生存者選択は上位でいいか。面倒だしNSGAにしたらやり方変わるし
        public virtual void SelectSurvivor()
        {
            List<MyAssignment> allCandidates = population.Concat(offspring).ToList();
            allCandidates.Sort((a, b) => a.ObjectiveFunctions[0].CompareTo(b.ObjectiveFunctions[0]));
            population = allCandidates.Take(population_size).Select(ind => new MyAssignment(ind)).ToList();
        }

        public virtual void MutateSingle(ref MyAssignment solution)
        {

            int r = randomCreater.Next(100);
            if (r < mutatePriority)
            {
                int customerId = randomCreater.Next(CustomerNum);
                int[] newGene = new int[CustomerNum * 2];
                int offset = 0;
                for (int i = 0; i < CustomerNum * 2; i++)
                {
                    if (solution.gene[0][i] / 2 != customerId)
                    {
                        newGene[i - offset] = solution.gene[0][i];
                    }
                    else
                    {
                        offset++;
                    }
                }

                newGene[CustomerNum * 2 - 2] = customerId * 2;
                newGene[CustomerNum * 2 - 1] = customerId * 2 + 1;
                
                for (int i = 0; i < CustomerNum * 2; i++)
                {
                    solution.gene[0][i] = newGene[i];
                }

            }
        }

        public virtual void MutateMulti(ref MyAssignment solution)
        {
            int r = randomCreater.Next(100);
            if (r < mutatePriority)
            {
                int customerId = randomCreater.Next(CustomerNum);
                for (int vehicleId = 0;  vehicleId < solution.gene.Count; vehicleId++)
                {
                    solution.gene[vehicleId].Remove(customerId * 2);
                    solution.gene[vehicleId].Remove(customerId * 2 + 1);
                }

                BestInsert(ref solution.gene, customerId);


            }
        }

        // 0, 1反転
        public virtual void FixRideOnOff(ref MyAssignment solution)
        {
            int[] alreadyDelivery = new int[CustomerNum];
            for (int i = 0; i < CustomerNum; i++)
            {
                alreadyDelivery[i] = -1;
            }
            for (int vehicleId = 0; vehicleId < solution.gene.Count; vehicleId++)
            {
                for (int i = 0; i < solution.gene[vehicleId].Count; i++)
                {
                    int customerId = solution.gene[vehicleId][i] / 2; ;
                    int pickupOrDelivery = solution.gene[vehicleId][i] % 2;
                    if (pickupOrDelivery == 0)
                    { // pickup
                        if (alreadyDelivery[customerId] != -1)
                        {
                            solution.gene[vehicleId][i] = 2 * customerId + 1;
                            solution.gene[vehicleId][alreadyDelivery[customerId]] = 2 * customerId;
                        }
                    }
                    else
                    {
                        alreadyDelivery[customerId] = i;
                    }
                }
            }
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

        // 制約違反の解は多数存在
        // GAで
        public virtual void InitialPopulationSingle()
        {
            population = new List<MyAssignment>();
            int[] numbers = new int[CustomerNum * 2 + VehicleNum - 1];
            for (int i = 0; i < CustomerNum * 2; i++)
            {
                numbers[i] = i;
            }
            for (int i = 0; i < population_size; i++)
            {
                // customer * 2の順列のシャッフル
                // 0, 1のあれは修正する。
                int[] solutionP = numbers.OrderBy(x => randomCreater.Next()).ToArray();
                MyAssignment firstSolution = new MyAssignment(CustomerNum);


                //firstSolution.VehicleRoutes[0] = new List<RouteStep>();
                firstSolution.gene.Add(new List<int>());
                for (int j = 0; j < CustomerNum * 2; j++)
                {
                    firstSolution.gene[0].Add(solutionP[j]);
                }
                FixRideOnOff(ref firstSolution);
                firstSolution.Simulate(DataModel);
                firstSolution.SetId(GiveId());
                population.Add(firstSolution);
            }
        }

        public virtual void InitialPopulationMulti()
        {
            population = new List<MyAssignment>();
            int[] customer = new int[CustomerNum];
            for (int i = 0; i < CustomerNum; i++)
            {
                customer[i] = i;
            }
            for (int i = 0; i < population_size; i++)
            {
                // customer * 2の順列のシャッフル
                // 0, 1のあれは修正する。
                int[] solutionP = customer.OrderBy(x => randomCreater.Next()).ToArray();
                MyAssignment firstSolution = new MyAssignment(CustomerNum);


                int m = randomCreater.Next(1, CustomerNum);
                List<List<int>> Group = new List<List<int>>();
                for (int j = 0; j < m; j++)
                {
                    Group.Add(new List<int>());
                    Group[j].Add(solutionP[j]);
                }
                for (int j = m; j < CustomerNum; j++)
                {
                    int v = randomCreater.Next(m);
                    Group[v].Add(solutionP[j]);
                }

                for (int j = 0; j < Group.Count(); j++)
                {
                    List<int> gr = Group[j];
                    int[] gr_p = new int[gr.Count * 2];
                    for (int k = 0; k < gr.Count; k++)
                    {
                        gr_p[k * 2] = gr[k] * 2;
                        gr_p[k * 2 + 1] = gr[k] * 2 + 1;
                    }
                    int[] order = gr_p.OrderBy(x => randomCreater.Next()).ToArray();
                    firstSolution.gene.Add(new List<int>());
                    for (int k = 0; k < order.Length; k++) {
                        firstSolution.gene[j].Add(order[k]);
                    }
                }

                FixRideOnOff(ref firstSolution);
                firstSolution.Simulate(DataModel);
                firstSolution.SetId(GiveId());
                population.Add(firstSolution);
            }
        }

        private void InsertRequestFeasibly(ref MyAssignment indiv, int cid, int startId, int exceptId)
        {
            int pickup = cid * 2;
            int delivery = cid * 2 + 1;

            if (indiv.gene.Count == 0)
            {
                indiv.gene.Add(new List<int> { pickup, delivery });
                return;
            }

            for (int r = 0; r < indiv.gene.Count; r++)
            {
                int r2 = (startId + r) % indiv.gene.Count;
                if (r2 == exceptId) continue;
                var route = indiv.gene[r2];

                for (int pPos = route.Count; pPos >= 0; pPos--)
                {
                    for (int dPos = pPos + 1; dPos <= route.Count + 1; dPos++)
                    {
                        route.Insert(pPos, pickup);
                        route.Insert(dPos, delivery);
                        long check = CheckRouteFeasible(DataModel, route);

                        if (check < 0)
                        {
                            return;
                        }

                        route.RemoveAt(dPos);
                        route.RemoveAt(pPos);
                    }
                }
            }

            indiv.gene.Add(new List<int> { pickup, delivery });
        }

        public virtual void MutateMultiConst(ref MyAssignment solution)
        {
            // ミューテーションが起こる確率
            if (randomCreater.Next(100) >= mutatePriority) return;

            if (solution.gene.Count == 0) return;

            int routeId = randomCreater.Next(solution.gene.Count);

            var route = solution.gene[routeId];

            int pos = randomCreater.Next(route.Count);
            int tmp = route[pos];
            int cid = tmp / 2;
            int pickup = cid * 2;
            int delivery = cid * 2 + 1;

            int orgId = -1;
            for (int v = 0; v < solution.gene.Count; v++)
            {
                if (solution.gene[v].Contains(pickup))
                {
                    orgId = v;
                    solution.gene[v].Remove(pickup);
                    solution.gene[v].Remove(delivery);
                    break;
                }
            }

            InsertRequestFeasibly(ref solution, cid, orgId + 1, orgId);
            solution.gene.RemoveAll(genev => genev == null || genev.Count == 0);
        }

        public long CheckRouteFeasible(RoutingDataModel DataModel, List<int> route)
        {
            int customerNum = DataModel.IndexManager.Customers.Count;
            int[] picked = new int[customerNum];
            int currentLoad = 0;

            int previousStopId = DataModel.Starts[0];
            int currentTime = 0;
            int totalTravelTime = 0;

            for (int i = 0; i < route.Count; i++)
            {
                int node = route[i];
                int customer = node / 2;
                int pd = node % 2;

                int stopId = ConvertIndex2LStopId(1, node);

                // ------ 移動時間で時刻更新 ------
                int travelTime = (int)DataModel.TravelTimes[previousStopId, stopId];
                currentTime += travelTime;
                totalTravelTime += travelTime;

                long desiredTW = DataModel.IndexManager.Customers[customer].DesiredTimeWindow[pd];

                if (currentTime < desiredTW)
                {
                    currentTime = (int)desiredTW;
                }

                if (pd == 0)
                {
                    picked[customer] = 1;
                    currentLoad++;
                }
                else
                {
                    if (picked[customer] == 0)
                    {
                        return -1;
                    }
                    currentLoad--;
                }

                if (currentLoad > Capacity)
                {
                    return -1;
                }

                long desired = DataModel.IndexManager.Customers[customer].DesiredTimeWindow[pd];
                long diff = currentTime - desired;

                if (pd == 0 && diff > MaxWait)
                {
                    return -1;
                }
                if (pd == 1 && diff > MaxDelay)
                {
                    return -1;
                }

                previousStopId = stopId;
            }

            return totalTravelTime;
        }

        public virtual void InitialPopulationConstGuided()
        {
            population = new List<MyAssignment>();

            int[] customers = Enumerable.Range(0, CustomerNum).ToArray();

            // 緊急度順ソート（ガイド）
            int[] sorted = customers
                .OrderBy(c => DataModel.IndexManager.Customers[c].RequestTime)
                .ToArray();

            for (int p = 0; p < population_size; p++)
            {
                MyAssignment indiv = new MyAssignment(CustomerNum);

                int m = randomCreater.Next(1, CustomerNum);

                int[] urgent = sorted.Take(m)
                    .OrderBy(x => randomCreater.Next())
                    .ToArray();

                foreach (int cid in urgent)
                {
                    int pickup = cid * 2;
                    int delivery = cid * 2 + 1;
                    indiv.gene.Add(new List<int> { pickup, delivery });
                }

                int[] rest = sorted.Skip(m)
                    .OrderBy(x => randomCreater.Next())
                    .ToArray();

                foreach (int cid in rest)
                {
                    int rnd = randomCreater.Next(indiv.gene.Count);
                    InsertRequestFeasibly(ref indiv, cid, rnd, -1);
                }

                FixRideOnOff(ref indiv);

                indiv.Simulate(DataModel);
                indiv.SetId(GiveId());
                population.Add(indiv);
            }
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


        public virtual MyAssignment TryGetSolution(string path, string objCase, string _crossOver, string _mutateKind, string _initalKind)
        {
            sw = Stopwatch.StartNew();
            crossOverKind = _crossOver;
            mutateKind = _mutateKind;
            initialKind = _initalKind;
            MyAssignment dummy = new MyAssignment(0);
            dummy.setPath(path);
            dummy.setObjCase(objCase);
            dummy.setGeneCnt(0);

            InitialPopulation();



            while (StoppingCondition() == 0)
            {
                CrossOver();
                SelectSurvivor();
                generationCnt++;
                dummy.setGeneCnt(generationCnt);
            }
            MyAssignment solution = population[0];

            return solution;
        }
    }
}
