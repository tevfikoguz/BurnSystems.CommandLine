
namespace BurnSystems.CommandLine
{
    using BurnSystems.CommandLine.ByAttributes;
    using BurnSystems.CommandLine.Helper;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;

    /// <summary>
    /// Evaluates the command line
    /// </summary>
    public class Parser
    {
        /// <summary>
        /// Stores the usage writer
        /// </summary>
        private UsageWriter usageWriter;

        /// <summary>
        /// Nichtbenannte Argument
        /// </summary>
        private List<string> unnamedArguments =
            new List<string>();

        /// <summary>
        /// Stores the argument information
        /// </summary>
        private List<ArgumentInfo> argumentInfos = new List<ArgumentInfo>();

        /// <summary>
        /// Named arguments
        /// </summary>
        private Dictionary<string, string> namedArguments =
            new Dictionary<string, string>();

        /// <summary>
        /// Stores the definitions, which will be replaced
        /// </summary>
        private List<ICommandLineFilter> filters =
            new List<ICommandLineFilter>();

        /// <summary>
        /// Stores the value whether the parsing already had been executed
        /// </summary>
        private bool isParsed;

        /// <summary>
        /// Stores the arguments as given in the constructor
        /// </summary>
        private string[] arguments;

        /// <summary>
        /// Stores the errors which were created during parsing
        /// </summary>
        private List<string> errors = new List<string>();

        /// <summary>
        /// Gets the errors which occured during parsing
        /// </summary>
        internal List<string> Errors
        {
            get { return this.errors; }
        }

        /// <summary>
        /// Returns the argument information
        /// </summary>
        internal IEnumerable<ArgumentInfo> ArgumentInfos
        {
            get { return this.argumentInfos; }
        }

        /// <summary>
        /// Gets all argument infos, which describe the named arguments
        /// </summary>
        internal IEnumerable<NamedArgumentInfo> NamedArgumentInfos
        {
            get
            {
                return this.ArgumentInfos
                    .Select(x => x as NamedArgumentInfo)
                    .Where(x => x != null);
            }
        }

        /// <summary>
        /// Gets all argument infos, which describe the unnamed arguments
        /// </summary>
        internal IEnumerable<UnnamedArgumentInfo> UnnamedArgumentInfos
        {
            get
            {
                return this.ArgumentInfos
                    .Select(x => x as UnnamedArgumentInfo)
                    .Where(x => x != null);
            }
        }

        /// <summary>
        /// Gets a list of unnamed arguments
        /// </summary>
        public List<string> UnnamedArguments
        {
            get
            {
                if (!this.isParsed)
                {
                    this.Parse();
                }

                return this.unnamedArguments;
            }
        }

        /// <summary>
        /// Gets a dictionary of named arguments
        /// </summary>
        public Dictionary<string, string> NamedArguments
        {
            get
            {
                if (!this.isParsed)
                {
                    this.Parse();
                }

                return this.namedArguments;
            }
        }

        /// <summary>
        /// Parses the value into the a new instance of the given object type
        /// </summary>
        /// <typeparam name="T">Type to be created </typeparam>
        /// <returns>The created type, which received the argument</returns>
        /// <remarks>This class somehow violates the layering between Parser and the 
        /// attribute-driven parser. To ease the use of the attribute-driven parser, 
        /// the static method is included here and not in the class ByAttributeParser</remarks>
        public static T ParseIntoOrShowUsage<T>(string[] args) where T : class, new()
        {
            var byAttributeParser = new ByAttributeParser<T>();
            var parser = byAttributeParser.PrepareParser(args);
            parser.Parse();

            if (parser.ShowUsageIfNecessary())
            {
                return null;
            }

            var result = byAttributeParser.FillObject();
            if (parser.ShowUsageIfNecessary())
            {
                return null;
            }

            return result;
        }

        /// <summary>
        /// Initializes a new instance of the CommandLineEvaluator class.
        /// </summary>
        /// <param name="arguments">List of program arguments</param>
        public Parser(string[] arguments)
        {
            Contract.Assert(arguments != null);
            Contract.EndContractBlock();

            this.arguments = arguments;

            this.usageWriter = new UsageWriter(this);

            this.filters.Add(new DefaultValueFilter());
            this.filters.Add(new RequiredFilter());
        }

        /// <summary>
        /// Initializes a new instance of the CommandLineEvaluator class.
        /// </summary>
        /// <param name="arguments">List of program arguments</param>
        /// <param name="definitions">List of definitions, that will define the argument structure to be parsed</param>
        public Parser(string[] arguments, IEnumerable<ICommandLineFilter> definitions)
            : this(arguments)
        {
            if (definitions != null)
            {
                this.filters.AddRange(definitions);
            }
        }

        /// <summary>
        /// Adds the information for one argument
        /// </summary>
        /// <param name="info">Information to be added</param>
        internal void AddArgumentInfo(ArgumentInfo info)
        {
            this.argumentInfos.Add(info);
        }

        /// <summary>
        /// Adds a filter to the evaluator. The filter will be called before and 
        /// after the parsing
        /// </summary>
        /// <param name="filter"></param>
        public void AddFilter(ICommandLineFilter filter)
        {
            this.filters.Add(filter);
        }

        /// <summary>
        /// Parses the arguments and shows the usage, if it the parsing did not complete.
        /// True, if parsing was successful
        /// </summary>
        /// <returns>True, when parsing was successful</returns>
        public bool ParseOrShowUsage()
        {
            this.Parse();

            return !this.ShowUsageIfNecessary();
        }

        /// <summary>
        /// Checks the help flags and the number of errors and shows
        /// the usage and exception if necessary
        /// </summary>
        /// <returns>true, if no usage was shown</returns>
        private bool ShowUsageIfNecessary()
        {
            if (this.errors.Count > 0)
            {
                this.usageWriter.ShowUsageAndException();
                return true;
            }

            if (this.NamedArguments.ContainsKey("help")
                || this.NamedArguments.ContainsKey("h")
                || this.NamedArguments.ContainsKey("?"))
            {
                this.usageWriter.ShowUsage();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks, if we have an error during parsing
        /// </summary>
        private void CheckForErrors()
        {
            if (!this.isParsed)
            {
                throw new InvalidOperationException("ParseOrShowUsage() is not called");
            }

            if (this.errors.Count > 0)
            {
                throw new InvalidOperationException("Errors occured during parsing");
            }
        }

        /// <summary>
        /// Parses the arguments
        /// </summary>
        /// <param name="arguments">Arguments to be parsed</param>
        private void Parse()
        {
            if (this.isParsed)
            {
                throw new InvalidOperationException("The arguments have been parsed already");
            }

            this.isParsed = true;

            foreach (var filter in filters)
            {
                filter.BeforeParsing(this);
            }

            // The actual parsing
            for (var n = 0; n < arguments.Length; n++)
            {
                var argument = arguments[n];
                if (string.IsNullOrEmpty(argument))
                {
                    // Do nothing, when the argument is empty
                    continue;
                }

                if (argument.StartsWith("--", StringComparison.Ordinal))
                {
                    // Default argument with long name
                    var argumentName = argument.Substring(2);
                    AddValueToNamedArgument(ref n, argumentName);
                }
                else if (argument[0] == '-')
                {
                    // Short argument
                    var argumentName = argument.Substring(1);

                    this.ParseSingleDashOption(ref n, argumentName);
                }
                else
                {
                    // No single-dash or multi-dash line
                    this.unnamedArguments.Add(argument);
                }
            }

            foreach (var filter in filters)
            {
                filter.AfterParsing(this);
            }
        }

        /// <summary>
        /// Parses a single dash option and 
        /// </summary>
        /// <param name="n">Position of the argument, at which we are currently.
        /// This will be updated, if arguments in future will be parsed</param>
        /// <param name="argumentName">Name of the argument to be parsed</param>
        private int ParseSingleDashOption(ref int n, string argumentName)
        {
            foreach (var cChar in argumentName)
            {
                // Check, if we have a mapping to a long name
                var info = this.NamedArgumentInfos.Where(x => x.ShortName == cChar).FirstOrDefault();
                if (info == null)
                {
                    // No, we don't have a mapping
                    this.NamedArguments[argumentName] = "1";
                }
                else if (!info.HasValue)
                {
                    // We have a mapping, but we have no value, so default to "1"
                    this.NamedArguments[info.LongName] = "1";
                }
                else
                {
                    // We have a mapping and a value, so the option shall be the only option
                    if (argumentName.Length > 1)
                    {
                        this.AddError("Shortname " + cChar + " has a value and is used with other options");
                    }

                    this.AddValueToNamedArgument(ref n, info.LongName);
                }
            }

            return n;
        }

        /// <summary>
        /// Adds a value to a named argument
        /// </summary>
        /// <param name="n">Position, where the argument name was found</param>
        /// <param name="argumentName">Name of the argument being used</param>
        /// <returns>The new position to be used for the parsing</returns>
        private void AddValueToNamedArgument(ref int n, string argumentName)
        {
            // Supports the named arguments with values
            var info =
                this.NamedArgumentInfos.Where(x => x.LongName.ToLower() == argumentName.ToLower()).FirstOrDefault();

            if (info == null)
            {
                this.namedArguments[argumentName] = "1";
            }
            else if (!info.HasValue)
            {
                this.namedArguments[info.LongName] = "1";
            }
            else
            {
                n++;
                if (arguments.Length <= n)
                {
                    this.AddError(
                        "Value missing for parameter: " + argumentName);
                }
                else
                {
                    this.namedArguments[info.LongName] = arguments[n];
                }
            }
        }

        /// <summary>
        /// Adds an error to the parsing
        /// </summary>
        /// <param name="error">Error to be added</param>
        public void AddError(string error)
        {
            this.errors.Add(error);
        }
    }
}
