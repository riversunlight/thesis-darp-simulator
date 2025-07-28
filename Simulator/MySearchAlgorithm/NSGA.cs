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
using Simulator.Logger;
using System.IO;

namespace Simulator.MySearchAlgorithm
{

    // 簡単化のため一旦GAにする(目的関数は1個目にしよう)

    public class NSGA
    {
        public RoutingModel RoutingModel;
        public RoutingIndexManager Manager;
        public RoutingDataModel DataModel;
        public int population_size;
        public int offspring_size;
        public List<MyAssignment> population;
        public List<MyAssignment> offspring;
        int generationCnt;
        int evolutionCnt;
        Random randomCreater;
        int VehicleNum;
        int NodeNum;
        int CustomerNum;
        int mutatePriority;

        public NSGA()
        {
            population_size = 60;
            offspring_size = 60;
            mutatePriority = 10;
            randomCreater = new Random();
        }


        // 親は毎回選べるように
        public void CrossOver()
        {
            offspring = new List<MyAssignment>();
            for (int i = 0; i < offspring_size / 2; i++)
            {
                int p1 = -1, p2 = -1;
                SelectParent(ref p1, ref p2);
                int[] p1_gene = population[p1].gene;
                int[] p2_gene = population[p2].gene;

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
                            long bestScore = 100000000;
                            List<int> bestChildGene = null;
                            for (int pickupPoint = 0; pickupPoint < childGeneList.Count; pickupPoint++)
                            {
                                childGeneList.Insert(pickupPoint, customerId * 2);
                                for (int deliveryPoint = pickupPoint + 1; deliveryPoint < childGeneList.Count; deliveryPoint++)
                                {
                                    childGeneList.Insert(deliveryPoint, customerId * 2 + 1);
                                    MyAssignment tempSolution = new MyAssignment(CustomerNum);
                                    for (int k = 0; k < p1_gene.Length; k++)
                                    {
                                        if (k < childGeneList.Count)
                                        {
                                            tempSolution.gene[k] = childGeneList[k];
                                        }
                                        else
                                        {
                                            tempSolution.gene[k] = -1;
                                        }
                                    }
                                    tempSolution.Simulate(DataModel);
                                    if (tempSolution.ObjectiveFunctions[1] < bestScore)
                                    {
                                        bestScore = tempSolution.ObjectiveFunctions[1];
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
                        offspringSolution.gene[k] = childGeneList[k];
                    }
                    offspringSolution.Simulate(DataModel);
                    Mutate(ref offspringSolution);
                    offspring.Add(offspringSolution);
                }

            }
        }

        // 一様にするか
        public void SelectParent(ref int p1, ref int p2)
        {
            p1 = randomCreater.Next(population_size);
            p2 = randomCreater.Next(population_size - 1);
            if (p1 == p2) p2 = population_size - 1;
        }

        // 生存者選択: 支配度からランクを割り出し、ランク0を次の親世代に入れる。
        public void SelectSurvivor()
        {
            List<MyAssignment> allCandidates = population.Concat(offspring).ToList();
            int[] dominatedNum = new int[allCandidates.Count]; // 支配度管理
            List<List<int>> edges = new List<List<int>>(); // 後から支配度減らす用
            for (int i = 0; i < allCandidates.Count;i++)
            {
                edges.Add(new List<int>());
            }
            population = new List<MyAssignment>();
            for (int i = 0; i < allCandidates.Count; i++)
            {
                for (int j = 0; j < allCandidates.Count; j++)
                {
                    if (allCandidates[i].ObjectiveFunctions[0] < allCandidates[j].ObjectiveFunctions[0]
                        && allCandidates[i].ObjectiveFunctions[1] < allCandidates[j].ObjectiveFunctions[1])
                    {
                        dominatedNum[j] += 1;
                        edges[i].Add(j);
                    }
                    if (allCandidates[j].ObjectiveFunctions[0] < allCandidates[i].ObjectiveFunctions[0]
                        && allCandidates[j].ObjectiveFunctions[1] < allCandidates[i].ObjectiveFunctions[1])
                    {
                        dominatedNum[i] += 1;
                        edges[j].Add(i);
                    }
                }
            }
            List<MyAssignment> nextPopulation = new List<MyAssignment>();
            while (nextPopulation.Count < population_size)
            {
                List<MyAssignment> rank0Solutions = new List<MyAssignment>();
                for (int i = 0; i < allCandidates.Count; i++)
                {
                    if (dominatedNum[i] == 0) {
                        rank0Solutions.Add(allCandidates[i]);
                        foreach (int idx in edges[i]) {
                            dominatedNum[idx] -= 1;
                        }
                    }
                    if (rank0Solutions.Count + nextPopulation.Count <= population_size)
                    {
                        nextPopulation = nextPopulation.Concat(rank0Solutions).ToList();
                    } else
                    {
                        // 混雑度ソート追加
                        for (int j = 0; j < rank0Solutions.Count; j++)
                        {
                            if (nextPopulation.Count >= population_size) break;
                            nextPopulation.Add(rank0Solutions[j]);
                        }
                    }
                }
            }
            population = nextPopulation;
        }

        public void Mutate(ref MyAssignment solution)
        {
            int r = randomCreater.Next(100);
            if (r < mutatePriority)
            {
                int customerId = randomCreater.Next(CustomerNum);
                int[] newGene = new int[solution.gene.Length];
                int offset = 0;
                for (int i = 0; i < solution.gene.Length; i++)
                {
                    if (solution.gene[i] / 2 != customerId)
                    {
                        newGene[i - offset] = solution.gene[i];
                    }
                    else
                    {
                        offset++;
                    }
                }
                newGene[newGene.Length - 2] = customerId * 2;
                newGene[newGene.Length - 1] = customerId * 2 + 1;
                solution.gene = newGene;
            }
            solution.Simulate(DataModel);
        }

        // 0, 1反転
        public void FixRideOnOff(ref MyAssignment solution)
        {
            int[] alreadyDelivery = new int[CustomerNum];
            for (int i = 0; i < CustomerNum; i++)
            {
                alreadyDelivery[i] = -1;
            }
            for (int vehicleId = 0; vehicleId < Manager.GetNumberOfVehicles(); vehicleId++)
            {
                for (int i = 0; i < solution.gene.Length; i++)
                {
                    int customerId = solution.gene[i] / 2; ;
                    int pickupOrDelivery = solution.gene[i] % 2;
                    if (pickupOrDelivery == 0)
                    { // pickup
                        if (alreadyDelivery[customerId] != -1)
                        {
                            solution.gene[i] = 2 * customerId + 1;
                            solution.gene[alreadyDelivery[customerId]] = 2 * customerId;
                        }
                    }
                    else
                    {
                        alreadyDelivery[customerId] = i;
                    }
                }
            }
        }

        // 制約違反の解は多数存在
        // GAで
        public void InitialPopulation()
        {
            population = new List<MyAssignment>();
            int[] numbers = new int[CustomerNum * 2];
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

                firstSolution.VehicleRoutes[0] = new List<RouteStep>();
                for (int j = 0; j < CustomerNum * 2; j++)
                {
                    firstSolution.gene[j] = solutionP[j];
                }
                FixRideOnOff(ref firstSolution);
                firstSolution.Simulate(DataModel);
                population.Add(firstSolution);
            }
        }

        public int StoppingCondition()
        {
            if (generationCnt == 10) return 1;
            return 0;
        }

        // 解を出力
        public void OutputSolutions()
        {
            FileRecorder recoder = new FileRecorder(Path.Combine(@Path.Combine(Environment.CurrentDirectory, @"Logger", @"MultiObjectSolution.csv")));
            int cnt = 0;
            int[] dominatedNum = new int[population.Count]; // 支配度管理
            List<List<int>> edges = new List<List<int>>(); // 後から支配度減らす用
            for (int i = 0; i < population.Count; i++)
            {
                edges.Add(new List<int>());
            }
            for (int i = 0; i < population.Count; i++)
            {
                for (int j = 0; j < population.Count; j++)
                {
                    if (population[i].ObjectiveFunctions[0] < population[j].ObjectiveFunctions[0]
                        && population[i].ObjectiveFunctions[1] < population[j].ObjectiveFunctions[1])
                    {
                        dominatedNum[j] += 1;
                        edges[i].Add(j);
                    }
                    if (population[j].ObjectiveFunctions[0] < population[i].ObjectiveFunctions[0]
                        && population[j].ObjectiveFunctions[1] < population[i].ObjectiveFunctions[1])
                    {
                        dominatedNum[i] += 1;
                        edges[j].Add(i);
                    }
                }
            }
            int rank = 0;
            while (cnt < population_size)
            {
                recoder.Record($"rank, {rank}");
                for (int i = 0; i < population.Count; i++)
                {
                    if (dominatedNum[i] == 0)
                    {
                        foreach (int idx in edges[i])
                        {
                            dominatedNum[idx] -= 1;
                        }
                    }
                    cnt++;
                    recoder.Record($"{population[i].ObjectiveFunctions[0]}, {population[i].ObjectiveFunctions[1]}");
                }
                rank++;
            }
        }


        public MyAssignment TryGetSolution(RoutingModel _routingModel, RoutingIndexManager _manager, RoutingDataModel _DataModel)
        {
            RoutingModel = _routingModel;
            Manager = _manager;
            DataModel = _DataModel;

            VehicleNum = Manager.GetNumberOfVehicles();
            NodeNum = Manager.GetNumberOfNodes();
            CustomerNum = DataModel.PickupsDeliveries.Length;

            MyAssignment solution = null;
            InitialPopulation();



            while (StoppingCondition() == 0)
            {
                CrossOver();
                SelectSurvivor();
                generationCnt++;

            }
            OutputSolutions();
            solution = population[0];

            return solution;
        }
    }
}
