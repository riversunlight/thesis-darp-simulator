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
using System.Diagnostics;

namespace Simulator.MySearchAlgorithm
{

    // 一番メインのNSGA

    public class NSGA : BaseGA
    {
        public NSGA(RoutingModel _routingModel, RoutingIndexManager _manager, RoutingDataModel _DataModel) : base(_routingModel, _manager, _DataModel)
        {
        }
        public void SettingsParameter()
        {
            population_size = 100;
            offspring_size = 100;
        }


        // 生存者選択: 支配度からランクを割り出し、ランク0を次の親世代に入れる。
        public override void SelectSurvivor()
        {
            using (StreamWriter writer = new StreamWriter("NSGA_debug.txt", append: true))
            {
                writer.WriteLine(generationCnt);
            }
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
                List<int> rank0Solutions = new List<int>();
                for (int i = 0; i < allCandidates.Count; i++)
                {
                    if (dominatedNum[i] == 0)
                    {
                        rank0Solutions.Add(i);
                        foreach (int idx in edges[i])
                        {
                            dominatedNum[idx] -= 1;
                        }
                        dominatedNum[i] = -1;
                    }
                }
                /*
                using (StreamWriter writer = new StreamWriter("NSGA_debug.txt", append: true))
                {
                    writer.WriteLine(nextPopulation.Count);
                    for (int i = 0; i < allCandidates.Count; i++)
                    {
                        writer.Write(dominatedNum[i] + ",");
                    }
                    writer.WriteLine();
                    for (int i = 0; i < rank0Solutions.Count; i++)
                    {
                        writer.Write(rank0Solutions[i] + ",");
                    }
                    writer.WriteLine();
                }*/

                if (rank0Solutions.Count + nextPopulation.Count <= population_size)
                {
                    foreach (int index in rank0Solutions)
                    {
                        nextPopulation.Add(new MyAssignment(allCandidates[index]));
                    }
                } else {
                    Dictionary<int, double> crowding = CalcCrowdingDistance(rank0Solutions, allCandidates);

                    var sorted = rank0Solutions
                        .OrderByDescending(idx => crowding[idx])
                        .ToList();

                    foreach (int index in sorted)
                    {
                        if (nextPopulation.Count >= population_size) break;
                        nextPopulation.Add(new MyAssignment(allCandidates[index]));
                    }
                }
            }
            population = nextPopulation;

        }

        private Dictionary<int, double> CalcCrowdingDistance(List<int> indices, List<MyAssignment> allCandidates)
        {
            int m = allCandidates[0].ObjectiveFunctions.Count;
            Dictionary<int, double> distance = indices.ToDictionary(i => i, i => 0.0);

            for (int obj = 0; obj < m; obj++)
            {
                var sorted = indices.OrderBy(i => allCandidates[i].ObjectiveFunctions[obj]).ToList();
                double minVal = allCandidates[sorted.First()].ObjectiveFunctions[obj];
                double maxVal = allCandidates[sorted.Last()].ObjectiveFunctions[obj];
                double range = maxVal - minVal;
                if (range == 0) range = 1; // ゼロ割防止

                // 両端は無限大
                distance[sorted.First()] = double.PositiveInfinity;
                distance[sorted.Last()] = double.PositiveInfinity;

                for (int k = 1; k < sorted.Count - 1; k++)
                {
                    double prev = allCandidates[sorted[k - 1]].ObjectiveFunctions[obj];
                    double next = allCandidates[sorted[k + 1]].ObjectiveFunctions[obj];
                    distance[sorted[k]] += (next - prev) / range;
                }
            }
            return distance;
        }


        public override MyAssignment TryGetSolution(string path, string objCase)
        {
            sw = Stopwatch.StartNew();

            MyAssignment dummy = new MyAssignment(0);
            dummy.setPath(path);
            dummy.setObjCase(objCase);
            dummy.setGeneCnt(0);
            SettingsParameter();
            InitialPopulation();
            generationCnt++; // 初期化を0世代とするため。
            dummy.setGeneCnt(generationCnt);
            while (StoppingCondition() == 0)
            {
                CrossOver();
                SelectSurvivor();
                generationCnt++;
                dummy.setGeneCnt(generationCnt);
            }
            MyAssignment solution = population[0];

            for (int i = 0; i < population_size; i++)
            {
                population[i].VisualTextSimulateResult(i, "NSGA");
            }

            return solution;
        }
    }
}
