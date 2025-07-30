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

    // 一番メインのNSGA

    public class NSGA : BaseGA
    {
        public NSGA(RoutingModel _routingModel, RoutingIndexManager _manager, RoutingDataModel _DataModel) : base(_routingModel, _manager, _DataModel)
        {
        }



        // 生存者選択: 支配度からランクを割り出し、ランク0を次の親世代に入れる。
        public override void SelectSurvivor()
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
                if (rank0Solutions.Count + nextPopulation.Count <= population_size)
                {
                    foreach (int index in rank0Solutions)
                    {
                        Console.Write(index + ",");
                        nextPopulation.Add(new MyAssignment(allCandidates[index]));
                    }
                    Console.WriteLine();
                } else {
                    // 混雑度ソート追加
                    for (int j = 0; j < rank0Solutions.Count; j++)
                    {
                        if (nextPopulation.Count >= population_size) break;
                        int index = rank0Solutions[j];
                        Console.Write(index + ",");

                        nextPopulation.Add(new MyAssignment(allCandidates[index]));
                    }
                }
            }
            population = nextPopulation;
        }


        public override MyAssignment TryGetSolution()
        {
            MyAssignment solution = null;
            InitialPopulation();
            OutputSolutionData("NSGA.csv", 0);

            while (StoppingCondition() == 0)
            {
                CrossOver();
                SelectSurvivor();
                generationCnt++;
                OutputSolutionData("NSGA.csv", generationCnt);
            }
            solution = population[0];

            return solution;
        }
    }
}
