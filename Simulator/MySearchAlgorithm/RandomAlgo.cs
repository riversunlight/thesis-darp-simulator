using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Google.OrTools.ConstraintSolver;
using Google.OrTools.Sat;
using Simulator.Objects.Data_Objects.Routing;
using Simulator.Objects.Simulation;



// ランダムな解を返す関数
namespace Simulator.MySearchAlgorithm
{
    public class RandomAlgo
    {
        RoutingModel RoutingModel;
        RoutingIndexManager Manager;
        RoutingDataModel DataModel;
        int CustomerNum;
        Random randomCreater;
        List<MyAssignment> population;
        int population_size = 0;
        int FirstWrited = 0;

        public RandomAlgo()
        {
            population_size = 20;
            randomCreater = new Random();
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

        // オブジェクト関数
        public void PrintStaticsData()
        {
            long[] mx_val = new long[population[0].ObjectiveFunctions.Count];
            long[] mn_val = new long[population[0].ObjectiveFunctions.Count];
            long[] sum_val = new long[population[0].ObjectiveFunctions.Count];
            for (int j = 0; j < population[0].ObjectiveFunctions.Count; j++)
            {
                mn_val[j] = 100000000;
            }
            for (int i = 0; i < population_size; i++)
            {
                for (int j = 0; j < population[i].ObjectiveFunctions.Count; j++)
                {
                    sum_val[j] += population[i].ObjectiveFunctions[j];
                    if (population[i].ObjectiveFunctions[j] > mx_val[j])
                    {
                        mx_val[j] = population[i].ObjectiveFunctions[j];
                    }
                    if (population[i].ObjectiveFunctions[j] < mn_val[j])
                    {
                        mn_val[j] = population[i].ObjectiveFunctions[j];
                    }
                }
            }
            long[] avg_val = new long[population[0].ObjectiveFunctions.Count];
            for (int j = 0; j < avg_val.Length; j++)
            {
                avg_val[j] = sum_val[j] / population_size;
            }
            long[] sum_error2_val = new long[population[0].ObjectiveFunctions.Count];

            for (int i = 0; i < population_size; i++)
            {
                for (int j = 0; j < population[i].ObjectiveFunctions.Count; j++)
                {
                    sum_error2_val[j] += (population[i].ObjectiveFunctions[j] - avg_val[j]) * (population[i].ObjectiveFunctions[j] - avg_val[j]);
                }
            }
            long[] various = new long[population[0].ObjectiveFunctions.Count];
            for (int j = 0; j < avg_val.Length; j++)
            {
                various[j] = sum_error2_val[j] / population_size;
            }
            for (int j = 0; j < avg_val.Length; j++)
            {
                Console.WriteLine("ObjectFuncID:" +  j);
                Console.WriteLine("Min: " +  mn_val[j]);
                Console.WriteLine("Max: " + mx_val[j]);
                Console.WriteLine("Avg: " + avg_val[j]);
                Console.WriteLine("Var: " + various[j]);

            }
        }
        
        public void OutputSolutionData(int gene_cnt)
        {
            string path = "randomAlgoSolutions.csv";
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

        public MyAssignment TryGetSolution(RoutingModel _routingModel, RoutingIndexManager _manager, RoutingDataModel _DataModel)
        {
            RoutingModel = _routingModel;
            Manager = _manager;
            DataModel = _DataModel;


            int vehicle_num = Manager.GetNumberOfVehicles();
            int node_num = Manager.GetNumberOfNodes();
            CustomerNum = DataModel.PickupsDeliveries.Length;
            // 評価回数リセット
            MyAssignment dummy = new MyAssignment(CustomerNum);
            dummy.resetEvalCnt();

            InitialPopulation();

            Console.WriteLine("vehicle_num:" + vehicle_num + "\\n");
            Console.WriteLine("node_num:" + node_num + "\\n");

            MyAssignment solution = population[0];

            PrintStaticsData();
            for (int i  = 0; i < population_size; i++)
            {
                population[i].VisualTextSimulateResult(i);
            }
            OutputSolutionData(0);
            

            return solution;
        }
    }
}
