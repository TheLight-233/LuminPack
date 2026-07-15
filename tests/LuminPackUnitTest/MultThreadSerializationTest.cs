using LuminPack;
using LuminPack.Attribute;

namespace LuminPackUnitTest
{
    public class MultiThreadSerializationTest
    {

        public static void TestMultiThreadCodeGenSerialize(List<string> results)
        {
            try
            {
                Console.WriteLine("Running MultiThreadCodeGenSerialize test...");
                
                C c = new C()
                {
                    Name = "test",
                    As = new List<A>()
                    {
                        new A() { Val = 1 },
                        new A() { Val = 2 },
                        new A() { Val = 3 }
                    }
                };
                
                int tests = 100;
                int completedTasks = 0;
                object lockObj = new object();
                Exception taskException = null;

                void Test()
                {
                    try
                    {
                        for (int i = 0; i < tests; i++)
                        {
                            _ = LuminPackSerializer.Serialize(c);
                        }
                        
                        lock (lockObj)
                        {
                            completedTasks++;
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (lockObj)
                        {
                            taskException = ex;
                        }
                    }
                }

                // Run 8 parallel tasks
                var tasks = new Task[8];
                for (int i = 0; i < 8; i++)
                {
                    tasks[i] = Task.Run(Test);
                }

                // Wait for all tasks to complete
                Task.WaitAll(tasks);

                if (taskException != null)
                {
                    throw taskException;
                }

                // Final verification
                var buf2 = LuminPackSerializer.Serialize(c);
                C c2 = LuminPackSerializer.Deserialize<C>(buf2);
                
                Console.WriteLine($"c的名字: {c.Name}, c2的名字: {c2.Name}");
                Console.WriteLine($"c的As数量: {c.As.Count}, c2的As数量: {c2.As.Count}");
                Console.WriteLine($"c的As[0].Val: {c.As[0].Val}, c2的As[0].Val: {c2.As[0].Val}");
                Console.WriteLine($"c的As[1].Val: {c.As[1].Val}, c2的As[1].Val: {c2.As[1].Val}");
                Console.WriteLine($"c的As[2].Val: {c.As[2].Val}, c2的As[2].Val: {c2.As[2].Val}");
                
                // Verify deserialized data
                if (c.Name == c2.Name && 
                    c.As.Count == c2.As.Count && 
                    c.As[0].Val == c2.As[0].Val &&
                    c.As[1].Val == c2.As[1].Val &&
                    c.As[2].Val == c2.As[2].Val)
                {
                    results.Add($"✓ MultiThreadCodeGenSerialize - PASSED (Completed {completedTasks}/8 tasks, {tests} serializations each)");
                }
                else
                {
                    results.Add("✗ MultiThreadCodeGenSerialize - FAILED (Data verification failed)");
                }
            }
            catch (Exception ex)
            {
                results.Add($"✗ MultiThreadCodeGenSerialize - ERROR: {ex.Message}");
            }
        }

        public static void TestMultiThreadCodeGenDeserialize(List<string> results)
        {
            try
            {
                Console.WriteLine("Running MultiThreadCodeGenDeserialize test...");
                
                C c = new C()
                {
                    Name = "test",
                    As = new List<A>()
                    {
                        new A() { Val = 1 },
                        new A() { Val = 2 },
                        new A() { Val = 3 }
                    }
                };
                
                byte[] buf = LuminPackSerializer.Serialize(c);
                int tests = 100;
                int completedTasks = 0;
                object lockObj = new object();
                Exception taskException = null;
                bool dataValid = true;

                void Test()
                {
                    try
                    {
                        for (int i = 0; i < tests; i++)
                        {
                            C c2 = LuminPackSerializer.Deserialize<C>(buf);
                            
                            // Verify data in each deserialization
                            if (c.Name != c2.Name || 
                                c.As.Count != c2.As.Count || 
                                c.As[0].Val != c2.As[0].Val ||
                                c.As[1].Val != c2.As[1].Val ||
                                c.As[2].Val != c2.As[2].Val)
                            {
                                lock (lockObj)
                                {
                                    dataValid = false;
                                }
                            }
                        }
                        
                        lock (lockObj)
                        {
                            completedTasks++;
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (lockObj)
                        {
                            taskException = ex;
                        }
                    }
                }

                // Run 8 parallel tasks
                var tasks = new Task[8];
                for (int i = 0; i < 8; i++)
                {
                    tasks[i] = Task.Run(Test);
                }

                // Wait for all tasks to complete
                Task.WaitAll(tasks);

                if (taskException != null)
                {
                    throw taskException;
                }

                if (dataValid)
                {
                    results.Add($"✓ MultiThreadCodeGenDeserialize - PASSED (Completed {completedTasks}/8 tasks, {tests} deserializations each)");
                }
                else
                {
                    results.Add("✗ MultiThreadCodeGenDeserialize - FAILED (Data validation failed in one or more threads)");
                }
            }
            catch (Exception ex)
            {
                results.Add($"✗ MultiThreadCodeGenDeserialize - ERROR: {ex.Message}");
            }
        }

        // Additional stress test with mixed operations
        public static void TestMixedMultiThreadOperations(List<string> results)
        {
            try
            {
                Console.WriteLine("Running MixedMultiThreadOperations test...");
                
                C originalData = new C()
                {
                    Name = "stress_test",
                    As = new List<A>()
                    {
                        new A() { Val = 10 },
                        new A() { Val = 20 },
                        new A() { Val = 30 },
                        new A() { Val = 40 }
                    }
                };

                int operationCount = 50;
                int completedTasks = 0;
                object lockObj = new object();
                Exception taskException = null;
                bool allDataValid = true;

                void MixedTest()
                {
                    try
                    {
                        var random = new Random();
                        
                        for (int i = 0; i < operationCount; i++)
                        {
                            // Randomly choose between serialize and deserialize
                            if (random.Next(2) == 0)
                            {
                                // Serialize operation
                                var data = new C()
                                {
                                    Name = $"test_{random.Next(100)}",
                                    As = new List<A>()
                                    {
                                        new A() { Val = random.Next(100) },
                                        new A() { Val = random.Next(100) }
                                    }
                                };
                                _ = LuminPackSerializer.Serialize(data);
                            }
                            else
                            {
                                // Deserialize operation with verification
                                byte[] serialized = LuminPackSerializer.Serialize(originalData);
                                C deserialized = LuminPackSerializer.Deserialize<C>(serialized);
                                
                                if (originalData.Name != deserialized.Name || 
                                    originalData.As.Count != deserialized.As.Count)
                                {
                                    lock (lockObj)
                                    {
                                        allDataValid = false;
                                    }
                                }
                            }
                        }
                        
                        lock (lockObj)
                        {
                            completedTasks++;
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (lockObj)
                        {
                            taskException = ex;
                        }
                    }
                }

                // Run multiple parallel tasks with mixed operations
                var tasks = new Task[6];
                for (int i = 0; i < 6; i++)
                {
                    tasks[i] = Task.Run(MixedTest);
                }

                // Wait for all tasks to complete
                Task.WaitAll(tasks);

                if (taskException != null)
                {
                    throw taskException;
                }

                if (allDataValid)
                {
                    results.Add($"✓ MixedMultiThreadOperations - PASSED (Completed {completedTasks}/6 tasks, {operationCount} mixed operations each)");
                }
                else
                {
                    results.Add("✗ MixedMultiThreadOperations - FAILED (Data validation failed in one or more threads)");
                }
            }
            catch (Exception ex)
            {
                results.Add($"✗ MixedMultiThreadOperations - ERROR: {ex.Message}");
            }
        }
    }
}