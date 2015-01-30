﻿using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BurnSystems.CommandLine.Test
{
    [TestFixture]
    public class ArgumentTests
    {
        [Test]
        public void TestNamedArgumentsWithValue()
        {
            var args = new string[] { "--input", "input.txt", "--output", "output.txt" };
            var evaluator = new CommandLineEvaluator(args)
                 .WithArgument("input", hasValue: true)
                 .WithArgument("output", hasValue: true);

            Assert.That(evaluator.NamedArguments.Count, Is.EqualTo(2));
            Assert.That(evaluator.UnnamedArguments.Count, Is.EqualTo(0));

            Assert.That(evaluator.NamedArguments["input"], Is.EqualTo("input.txt"));
            Assert.That(evaluator.NamedArguments["output"], Is.EqualTo("output.txt"));
        }
    }
}