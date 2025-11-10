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
            limitSeconds = 180;
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

        public void CrossOverOnePoint() // 1点交叉、O(n)
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
                        offspringSolution.gene[k] = childGeneList[k];
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
                int[] p1_gene = population[p1].gene;
                int[] p2_gene = population[p2].gene;

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
                    offspringSolution.gene = child;
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
                                        bestScore = tempSolution.ObjectiveFunctionFinishTime();
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
                    Mutate(ref offspringSolution);
                    offspringSolution.Simulate(DataModel);
                    offspringSolution.SetId(GiveId());
                    offspring.Add(offspringSolution);
                }

            }
        }

        public virtual void CrossOver()
        {
            CrossOverPPX();
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

        public virtual void Mutate(ref MyAssignment solution)
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
        }

        // 0, 1反転
        public virtual void FixRideOnOff(ref MyAssignment solution)
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
        public virtual void InitialPopulation()
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
                firstSolution.SetId(GiveId());
                population.Add(firstSolution);
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


        public virtual MyAssignment TryGetSolution()
        {
            sw = Stopwatch.StartNew();
            MyAssignment dummy = new MyAssignment(0);
            dummy.setPath("BaseGA.csv");
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
