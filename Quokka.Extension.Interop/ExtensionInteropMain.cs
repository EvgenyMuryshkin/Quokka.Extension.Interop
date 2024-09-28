using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Quokka.Extension.Interop
{
    public class ExtensionInteropMain
    {
        public virtual void PrintUsage()
        {
            Console.WriteLine($"Extension class should be marked with [ExtensionClass] attribute");
            Console.WriteLine($"Extension method should be marked with [ExtensionMethod] attribute");
            Console.WriteLine($"Extension method should be public static parameterless method");
            Console.WriteLine($"Extension method should return void, int, Task, Task<int>");
        }

        public virtual async Task<int> Invoke(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine($"Extension method was not specified");
                PrintUsage();
                return 1;
            }

            if (args[0].StartsWith("-"))
            {
                PrintUsage();
                return 1;
            }

            var extensionClasses = Assembly
                .GetEntryAssembly()
                .ExportedTypes
                .Where(t => t.GetCustomAttribute<ExtensionClassAttribute>() != null)
                .ToDictionary(t => t.Name);

            var invokeParts = args[0].Split('.');
            if (invokeParts.Length != 2)
            {
                Console.WriteLine($"Extension method should be in format 'Class.Method'. Provided value was '{args[0]}'");
                return 1;
            }

            if (!extensionClasses.TryGetValue(invokeParts[0], out var extensionClass))
            {
                Console.WriteLine($"Extension class was not found: {invokeParts[0]}");
                foreach (var c in extensionClasses.Keys)
                {
                    Console.WriteLine($"Available class: {c}");
                }
                return 1;
            }

            var extensionMethods = extensionClass
                .GetMethods()
                .Where(m => m.IsPublic && m.IsStatic && m.GetParameters().Length == 0 && m.Name == invokeParts[1])
                .ToDictionary(m => m.Name);

            if (!extensionMethods.TryGetValue(invokeParts[1], out var extensionMethod))
            {
                Console.WriteLine($"Extension method was not found on class '{extensionClass.Name}': {invokeParts[1]}");
                Console.WriteLine($"Extension method should be static method without parameters");

                foreach (var m in extensionMethods.Keys)
                {
                    Console.WriteLine($"Available method: {m}");
                }

                return 1;
            }

            var result = extensionMethod.Invoke(null, new object[] { });

            if (extensionMethod.ReturnType == typeof(void))
            {
                return 0;
            }
            else if (extensionMethod.ReturnType == typeof(int))
            {
                return (int)result;
            }
            else if (extensionMethod.ReturnType == typeof(Task))
            {
                var task = result as Task;
                await task;
                return 0;
            }
            else if (extensionMethod.ReturnType == typeof(Task<int>))
            {
                var task = result as Task<int>;
                return await task;
            }
            else
            {
                Console.WriteLine($"{args[0]}: return type is not supported: {extensionMethod.ReturnType}");
                PrintUsage();
                return 1;
            }
        }

        protected virtual Exception MostInnerException(Exception ex)
        {
            if (ex == null)
                return null;

            return MostInnerException(ex.InnerException) ?? ex;
        }

        protected virtual void ReportExceptionToConsole(Exception ex)
        {
            Console.WriteLine($"".PadRight(20, '='));
            Console.WriteLine($"{ex.GetType().Name}");
            Console.WriteLine($"{ex.Message}");
            Console.WriteLine($"{ex.StackTrace}");
        }

        protected virtual List<Exception> UnrollExceptions(Exception ex)
        {
            var result = new List<Exception>();
            if (ex == null)
                return result;

            switch (ex)
            {
                case TargetInvocationException tie:
                    {
                        result.AddRange(UnrollExceptions(tie.InnerException));
                    }
                    break;
                case AggregateException ae:
                    {
                        result.AddRange(UnrollExceptions(ae.InnerException));

                        if (ae.InnerExceptions != null)
                        {
                            result.AddRange(ae.InnerExceptions.SelectMany(e => UnrollExceptions(e)));
                        }
                    }
                    break;
                default:
                    {
                        result.Add(ex);
                        result.AddRange(UnrollExceptions(ex.InnerException));
                    }
                    break;
            }

            return result;
        }

        protected virtual void TraceException(Exception ex)
        {
            var unrolled = UnrollExceptions(ex);

            Console.WriteLine($"Extension method invocation failed");

            if (unrolled.Any())
            {
                Console.WriteLine($"Will display {unrolled.Count} exceptions");

                foreach (var exception in unrolled)
                {
                    ReportExceptionToConsole(exception);
                }
            }
            else
            {
                Console.WriteLine($"Exception information is not available");
            }
        }

        public virtual async Task<int> Run(string[] args)
        {
            var sw = new Stopwatch();
            sw.Start();

            try
            {
                Console.WriteLine($"InteropMain called: {string.Join(" ", args)}");
                return await Invoke(args);
            }
            catch (Exception ex)
            {
                TraceException(ex);
                return 1;
            }
            finally
            {
                Console.WriteLine($"InteropMain completed in {sw.ElapsedMilliseconds} ms: {string.Join(" ", args)}");
            }
        }

        public static async Task<int> Main(string[] args)
        {
            var runner = new ExtensionInteropMain();
            return await runner.Run(args);
        }
    }
}
