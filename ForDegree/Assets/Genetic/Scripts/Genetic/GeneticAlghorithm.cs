﻿using System;
using System.Collections.Generic;
namespace GeneticImplementation
{
    public class GeneticAlghorithm<T>
    {
        #region  Base GEnetics
        public List<DNA<T>> Population { get; private set; }
        public int Generation { get; private set; }
        public float BestFittnes { get; private set; }
        public T[] BestGenes { get; private set; }

        private float MutationRate;
        private Random random;
        private float fittnesSum;

        // Optimization
        private List<DNA<T>> newPopulation;

        // Add while in runtime
        public int dnaSize { get; private set; }
        private Func<T> getRandomGene;
        private Func<int, float> fittnesFunc;

        #endregion

        #region Second Type
        // 2. Part - Best individuals persist
        private int ElementsToKeep;
        #endregion

        #region  Third Type
        // 3.Part -  Stagnation of population/ Random Addition of genes, when in stagnation
        private float[] stagnationStats; // for derivative minimum is 2 points!
        private float[] FirstDerivativeStagnation;
        private float[] secondDeriv;

        private int watchForLast = 5;
        public int MinimumForWatching{
            get{
                return watchForLast;
            }
        }
        #endregion

        public GeneticAlghorithm() { }
        /// <summary>
        /// Construct new Generic Genetic Alghorithm with custom Fitness
        /// and generate random Gene functions
        /// </summary>
        public GeneticAlghorithm(
            int populationSize,
            int dnaSize,
            Random random,
            Func<T> getRandomGene,
            Func<int, float> fittnesFunc,
            int keepFirstNBEst,
            float mutationRate = 0.01f,
            bool useStagnation = false,
            int stepsToWatch = 5)
        {
            Generation = 1;
            MutationRate = mutationRate;

            Population = new List<DNA<T>>(populationSize);// optimization
            newPopulation = new List<DNA<T>>(populationSize); // optimization

            this.random = random;
            this.dnaSize = dnaSize;
            this.fittnesFunc = fittnesFunc;
            this.getRandomGene = getRandomGene;

            BestGenes = new T[dnaSize];

            for (int i = 0; i < populationSize; i++)
            {
                Population.Add(new DNA<T>(dnaSize, random, getRandomGene, fittnesFunc, true));
            }
            // 2. Type
            this.ElementsToKeep = keepFirstNBEst;

            // 3. Type
            if (useStagnation)
            {
                watchForLast = stepsToWatch;
                stagnationStats = new float[watchForLast];
                FirstDerivativeStagnation = new float[watchForLast];
                secondDeriv = new float[watchForLast];
            }
        }

        /// <summary>
        /// Construct new Generation
        /// </summary>
        public void NewGeneration(int NewElems = 0, bool crosOverNewElem = false, bool isItDelayed = false)
        {
            if (isItDelayed)
                return;
            // Add new Individuals while it is running
            int finalCount = Population.Count + NewElems;

            if (finalCount <= 0)
                return;

            if (Population.Count > 0)
            {
                CalculateFitnessForAll();
                Population.Sort(CompareDNA); // keep track of first N elements;
            }
            newPopulation.Clear();

            // Next generation of crossbreeding have the same size as previous
            for (int i = 0; i < finalCount; i++)
            {
                if (i < ElementsToKeep && i < Population.Count)
                {
                    newPopulation.Add(Population[i]);
                }
                else if (i < Population.Count || crosOverNewElem)
                {
                    // Critical moment!
                    DNA<T> parent1 = ChooseParent();
                    DNA<T> parent2 = ChooseParent();

                    DNA<T> child = parent1.CrossOver(parent2);

                    child.Mutate(MutationRate);
                    newPopulation.Add(child);
                }
                else
                {
                    newPopulation.Add(new DNA<T>(dnaSize, random, getRandomGene, fittnesFunc, true));
                }
            }

            // Optimization
            List<DNA<T>> tmpList = Population;
            Population = newPopulation;
            newPopulation = tmpList;
            Generation++;
        }

        public static int CompareDNA(DNA<T> a, DNA<T> b)
        {
            if (a.Fittnes > b.Fittnes)
            {
                return -1; // first argument comes first in the List
            }
            else if (a.Fittnes < b.Fittnes)
            {
                return 1;
            }
            return 0;
        }

        public void CalculateFitnessForAll()
        {
            fittnesSum = 0;
            // Find the best fittness
            DNA<T> best = Population[0];
            for (int i = 0; i < Population.Count; i++)
            {
                fittnesSum += Population[i].CalculateFitness(i);
                if (Population[i].Fittnes > best.Fittnes)
                {
                    best = Population[i];
                }
            }

            BestFittnes = best.Fittnes;
            best.Genes.CopyTo(BestGenes, 0);
        }


        private DNA<T> ChooseParent()
        {
            double randomNumber = random.NextDouble() * fittnesSum;
            for (int i = 0; i < Population.Count; i++)
            {
                if (randomNumber < Population[i].Fittnes)
                {
                    return Population[i];
                }
                randomNumber -= Population[i].Fittnes;
            }
            return null;
        }

        
        #region  Serialization
        public void SaveGeneration()
        {
            var saveTo = new GeneticSaveData<T>();
            saveTo.Generation = this.Generation;
            saveTo.AllGenesFromPopulation = new List<T[]>(Population.Count);
            for (int i = 0; i < Population.Count; i++)
            {
                saveTo.AllGenesFromPopulation.Add(new T[Population[i].Genes.Length]);
                Array.Copy(Population[i].Genes, saveTo.AllGenesFromPopulation[i], Population[i].Genes.Length);
            }
        }

        public void LoadGeneration()
        {

        }
        #endregion
        
        #region  Additional Genes and Stagnation Check
        // Figure out if the slope is at the peak, then maximum, when true do nothing
        // when false - add new gene, wait for a full round
        public bool IsStagnationAtPeak()
        {
            var result = false;
            if (stagnationStats.Length <= 1)
            {
                return result;
            }

            // Check if stagnation is at peak on last few rounds
            // 1. Calculate first derivative
            for (int i = 1; i < stagnationStats.Length; i++)
            {
                FirstDerivativeStagnation[i] = stagnationStats[i] - stagnationStats[i - 1]; // delta y/time (1);
            }

            // 2. Calculate second derivative
            for (int i = 1; i < FirstDerivativeStagnation.Length; i++)
            {
                secondDeriv[i] = FirstDerivativeStagnation[i] - FirstDerivativeStagnation[i - 1]; // delta y/time (1);
            }

            // Decide if Population was at peak on last few
            result = false;
            for (int i = 1; i < FirstDerivativeStagnation.Length; i++)
            {
                if (FirstDerivativeStagnation[i] == 0 && secondDeriv[i] < 0)
                {
                    // peak
                    result = true;
                }
            }

            return result;
        }

        // Always rotate with each generation
        private void rotateStagnationStats()
        {
            for (int i = 1; i < stagnationStats.Length; i++)
            {
                stagnationStats[i - 1] = stagnationStats[i];
                FirstDerivativeStagnation[i - 1] = FirstDerivativeStagnation[i];
                secondDeriv[i - 1] = secondDeriv[i];
            }

        }

        // Update value with each generation
        public void AddToStatgantion(float bestFitness)
        {
            rotateStagnationStats();
            stagnationStats[stagnationStats.Length - 1] = bestFitness;
        }


        public void addNewGenes(int newGenes = 2)
        {

            dnaSize = dnaSize + newGenes;
            var copyGenes = new T[dnaSize];
            for (int i = 0; i < BestGenes.Length; i++)
            {
                copyGenes[i] = BestGenes[i];
            }
            for (int i = BestGenes.Length; i < dnaSize; i++)
            {
                copyGenes[i] = getRandomGene();
            }

            BestGenes = copyGenes;
            newPopulation.Clear();

            // List<DNA<T>> copySamePop = new List<DNA<T>>();
            foreach (var individual in Population)
            {
                var currentGenes =  individual.Genes;
                var copyNew = new T[dnaSize];
                for (int i = 0; i < currentGenes.Length; i++)
                {
                    copyNew[i] = currentGenes[i];
                }

                for (int i = currentGenes.Length; i < dnaSize; i++)
                {
                    copyNew[i] = getRandomGene();
                }

                // set them back!
                individual.Genes = copyNew;
            }
        }
        #endregion
    }
}
