using System;
using System.Collections.Generic;
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


        public MyAssignment TryGetSolution(RoutingModel _routingModel, RoutingIndexManager _manager, RoutingDataModel _DataModel)
        {
            RoutingModel = _routingModel;
            Manager = _manager;
            DataModel = _DataModel;

            int vehicle_num = Manager.GetNumberOfVehicles();
            int node_num = Manager.GetNumberOfNodes();
            CustomerNum = DataModel.PickupsDeliveries.Length;

            InitialPopulation();

            Console.WriteLine("vehicle_num:" + vehicle_num + "\\n");
            Console.WriteLine("node_num:" + node_num + "\\n");

            MyAssignment solution = population[0];

            return solution;
        }
    }
}
