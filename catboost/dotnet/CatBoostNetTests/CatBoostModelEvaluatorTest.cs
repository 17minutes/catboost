using CatBoostNet;
using Deedle;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace CatBoostNetTests
{
    [TestClass]
    public class CatBoostModelEvaluatorTest
    {
        [TestMethod]
        public void RunIrisTest()
        {
            var workdir = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "datasets", "iris");
            var dsPath = Path.Combine(workdir, "iris.data");
            var df = Frame.ReadCsv(dsPath, hasHeaders: false);
            df.RenameColumns(new Collection<string>
            {
                "sepal length", "sepal width", "petal length", "petal width", "target"
            });
            var target = df.Rows.Select(obj => obj.Value["target"]).Values.Select(x => (string)x).ToArray();
            df.DropColumn("target");
            var data = df.ToArray2D<float>();

            var model = new CatBoostModelEvaluator(Path.Combine(workdir, "iris_model.cbm"));
            model.CatFeaturesIndices = new Collection<int> { };
            double[,] res = model.EvaluateBatch(data, new string[df.RowCount, 0]);

            string[] targetLabelList = new string[] { "Iris-setosa", "Iris-versicolor", "Iris-virginica" };
            for (int i = 0; i < res.GetLength(0); ++i)
            {
                int argmax = Enumerable.Range(0, res.GetLength(1)).Select(j => Tuple.Create(res[i, j], j)).Max().Item2;
                string predLabel = targetLabelList[argmax];
                Assert.IsTrue(predLabel == target[i], $"Iris test crashed on sample #{i+1}");
            }
        }

        [TestMethod]
        public void RunBostonTest()
        {
            var workdir = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "datasets", "boston");
            var dsPath = Path.Combine(workdir, "housing.data");

            List<float[]> featureList = new List<float[]>();
            List<double> targetList = new List<double>();
            using (TextReader textReader = new StreamReader(dsPath))
            {
                while (textReader.Peek() != -1)
                {
                    var tokens = textReader.ReadLine().Split(' ').ToList().Where(x => x != "");
                    targetList.Add(double.Parse(tokens.Last()));
                    featureList.Add(tokens.SkipLast(1).Select(x => float.Parse(x)).ToArray());
                }
            }

            if (featureList.Where(x => x.Length != featureList.First().Length).Any())
            {
                throw new InvalidDataException("Incosistent column count in housing.data");
            }

            double[] target = targetList.ToArray();
            float[,] features = new float[featureList.Count, featureList.First().Length];
            for (int i = 0; i < featureList.Count; ++i)
            {
                for (int j = 0; j < featureList.First().Length; ++j)
                {
                    features[i, j] = featureList[i][j];
                }
            }

            var model = new CatBoostModelEvaluator(Path.Combine(workdir, "boston_housing_model.cbm"));
            model.CatFeaturesIndices = new Collection<int> { };
            double[,] res = model.EvaluateBatch(features, new string[featureList.Count, 0]);

            var deltas = Enumerable.Range(0, featureList.Count).Select(i => new
            {
                Index = i + 1,
                LogDelta = Math.Abs(res[i, 0] - Math.Log(target[i])),
                Pred = Math.Exp(res[i, 0]),
                Target = target[i]
            });
            foreach (var delta in deltas)
                Console.WriteLine($"Sample #{delta.Index} / {deltas.Count()}, pred = {delta.Pred:0.00}, target = {delta.Target:0.00}");

            int totalErrors = deltas.Where(x => x.LogDelta >= .4).Count();
            Assert.IsTrue(
                totalErrors <= 7,
                $"Boston test crashed: expected <= 7 errors, got {totalErrors} error(s) on samples {{" +
                string.Join(
                    ", ",
                    deltas.Where(x => x.LogDelta >= .4).Take(8).Select(x => x.Index + 1)
                ) + ", ...}"
            );
        }

        [TestMethod]
        public void RunMushroomTest()
        {
            var workdir = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "datasets", "mushrooms");
            var dsPath = Path.Combine(workdir, "mushrooms.csv");
            var df = Frame.ReadCsv(dsPath);
            var target = df.Rows.Select(obj => obj.Value["class"]).Values.Select(x => (string)x).ToArray();
            df.DropColumn("class");
            var data = df.ToArray2D<string>();

            var model = new CatBoostModelEvaluator(Path.Combine(workdir, "mushroom_model.cbm"));
            model.CatFeaturesIndices = Enumerable.Range(0, df.ColumnCount).ToList();
            double[,] res = model.EvaluateBatch(new float[df.RowCount, 0], data);

            string[] targetLabelList = new string[] { "e", "p" };
            for (int i = 0; i < res.GetLength(0); ++i)
            {
                int argmax = res[i, 0] > 0 ? 1 : 0;
                string predLabel = targetLabelList[argmax];

                Assert.IsTrue(
                    predLabel == target[i],
                    $"Mushroom test crashed on sample {i + 1}"
                );
            }
        }
    }
}