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
    public class RandomAlgo: BaseGA
    {

        public RandomAlgo(RoutingModel _routingModel, RoutingIndexManager _manager, RoutingDataModel _DataModel) : base(_routingModel, _manager, _DataModel)
        {
        }

        public void SettingsParameter()
        {
            population_size = 20000;
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
        
        
        public override MyAssignment TryGetSolution(string path, string objCase)
        {
            // 評価回数リセット
            MyAssignment dummy = new MyAssignment(CustomerNum);
            dummy.setPath(path);
            dummy.setObjCase(objCase);
            dummy.resetEvalCnt();
            SettingsParameter();

            InitialPopulation();

            Console.WriteLine("vehicle_num:" + VehicleNum + "\\n");
            Console.WriteLine("node_num:" + NodeNum + "\\n");

            MyAssignment solution = population[0];

            PrintStaticsData();
            for (int i  = 0; i < population_size; i++)
            {
                population[i].VisualTextSimulateResult(i, "Random");
            }
            

            return solution;
        }
    }
}
