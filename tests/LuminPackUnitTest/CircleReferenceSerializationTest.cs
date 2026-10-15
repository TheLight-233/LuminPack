using LuminPack;
using LuminPack.Attribute;

namespace LuminPackUnitTest;

/// <summary>Provides circle reference serialization test functionality.</summary>
public class CircleReferenceSerializationTest
{
    /// <summary>Provides simple node functionality.</summary>
    [LuminPackable(GeneratorType.CircleReference)]
    public class SimpleNode
    {
        /// <summary>Gets or sets the name.</summary>
        [LuminPackOrder(0)] public string Name { get; set; } = string.Empty;
        /// <summary>Gets or sets the parent.</summary>
        [LuminPackOrder(1)] public SimpleNode? Parent { get; set; }
    }

    /// <summary>Verifies test simple circle reference.</summary>
    /// <param name="output">The destination receiving the result.</param>
    public static void TestSimpleCircleReference(List<string> output)
    {
        try
        {
            var node1 = new SimpleNode { Name = "Node1" };
            var node2 = new SimpleNode { Name = "Node2" };

            node1.Parent = node2;
            node2.Parent = node1;

            var buf = LuminPackSerializer.Serialize(node1);
            var deserializedNode1 = LuminPackSerializer.Deserialize<SimpleNode>(buf);

            if (deserializedNode1?.Parent?.Parent != null &&
                ReferenceEquals(deserializedNode1.Parent.Parent, deserializedNode1))
            {
                output.Add("✓ TestSimpleCircleReference - PASSED");
            }
            else
            {
                output.Add("✗ TestSimpleCircleReference - FAILED");
            }
        }
        catch (Exception ex)
        {
            output.Add($"✗ TestSimpleCircleReference - ERROR: {ex.Message}");
        }

        try
        {
            var node1 = new SimpleNode { Name = "Node1" };
            var node2 = new SimpleNode { Name = "Node2" };

            node1.Parent = node2;
            node2.Parent = node1;

            var buf = LuminPackSerializer.SerializeJson(node1);
            var deserializedNode1 = LuminPackSerializer.DeserializeJson<SimpleNode>(buf);

            if (deserializedNode1?.Parent?.Parent != null &&
                ReferenceEquals(deserializedNode1.Parent.Parent, deserializedNode1))
            {
                output.Add("✓ TestSimpleCircleReference [JSON] - PASSED");
            }
            else
            {
                output.Add("✗ TestSimpleCircleReference [JSON] - FAILED");
            }
        }
        catch (Exception ex)
        {
            output.Add($"✗ TestSimpleCircleReference [JSON] - ERROR: {ex.Message}");
        }
    }

    /// <summary>Verifies test simple reference.</summary>
    /// <param name="output">The destination receiving the result.</param>
    public static void TestSimpleReference(List<string> output)
    {
        try
        {
            var parent = new SimpleNode { Name = "Parent" };
            var child = new SimpleNode { Name = "Child", Parent = parent };

            var buf = LuminPackSerializer.Serialize(child);
            var deserializedChild = LuminPackSerializer.Deserialize<SimpleNode>(buf);

            if (deserializedChild?.Parent != null && deserializedChild.Parent.Name == "Parent")
            {
                output.Add("✓ TestSimpleReference - PASSED");
            }
            else
            {
                output.Add("✗ TestSimpleReference - FAILED");
            }
        }
        catch (Exception ex)
        {
            output.Add($"✗ TestSimpleReference - ERROR: {ex.Message}");
        }

        try
        {
            var parent = new SimpleNode { Name = "Parent" };
            var child = new SimpleNode { Name = "Child", Parent = parent };

            var buf = LuminPackSerializer.SerializeJson(child);
            var deserializedChild = LuminPackSerializer.DeserializeJson<SimpleNode>(buf);

            if (deserializedChild?.Parent != null && deserializedChild.Parent.Name == "Parent")
            {
                output.Add("✓ TestSimpleReference [JSON] - PASSED");
            }
            else
            {
                output.Add("✗ TestSimpleReference [JSON] - FAILED");
            }
        }
        catch (Exception ex)
        {
            output.Add($"✗ TestSimpleReference [JSON] - ERROR: {ex.Message}");
        }
    }

    /// <summary>Provides tree node functionality.</summary>
    [LuminPackable(GeneratorType.CircleReference)]
    public class TreeNode
    {
        /// <summary>Gets or sets the name.</summary>
        [LuminPackOrder(0)] public string Name { get; set; } = string.Empty;
        /// <summary>Gets or sets the parent.</summary>
        [LuminPackOrder(1)] public TreeNode? Parent { get; set; }
        /// <summary>Gets or sets the first child.</summary>
        [LuminPackOrder(2)] public TreeNode? FirstChild { get; set; }
        /// <summary>Gets or sets the sibling.</summary>
        [LuminPackOrder(3)] public TreeNode? Sibling { get; set; }
    }

    /// <summary>Verifies test complex circle reference.</summary>
    /// <param name="output">The destination receiving the result.</param>
    public static void TestComplexCircleReference(List<string> output)
    {
        try
        {
            var root = new TreeNode { Name = "Root" };
            var child1 = new TreeNode { Name = "Child1" };
            var child2 = new TreeNode { Name = "Child2" };

            // Represent the tree with FirstChild and Sibling links instead of a List.
            root.FirstChild = child1;
            child1.Sibling = child2;
            child1.Parent = root;
            child2.Parent = root;
            child1.Sibling = child2;
            child2.Sibling = child1;

            var buf = LuminPackSerializer.Serialize(root);
            var deserializedRoot = LuminPackSerializer.Deserialize<TreeNode>(buf);

            var deserializedChild1 = deserializedRoot?.FirstChild;
            var deserializedChild2 = deserializedRoot?.FirstChild?.Sibling;

            if (deserializedRoot != null &&
                deserializedChild1 != null &&
                deserializedChild2 != null &&
                ReferenceEquals(deserializedChild1.Sibling, deserializedChild2) &&
                ReferenceEquals(deserializedChild2.Sibling, deserializedChild1) &&
                ReferenceEquals(deserializedChild1.Parent, deserializedRoot) &&
                ReferenceEquals(deserializedChild2.Parent, deserializedRoot))
            {
                output.Add("✓ TestComplexCircleReference - PASSED");
            }
            else
            {
                output.Add("✗ TestComplexCircleReference - FAILED");
            }
        }
        catch (Exception ex)
        {
            output.Add($"✗ TestComplexCircleReference - ERROR: {ex.Message}");
        }

        try
        {
            var root = new TreeNode { Name = "Root" };
            var child1 = new TreeNode { Name = "Child1" };
            var child2 = new TreeNode { Name = "Child2" };

            // Represent the tree with FirstChild and Sibling links instead of a List.
            root.FirstChild = child1;
            child1.Sibling = child2;
            child1.Parent = root;
            child2.Parent = root;
            child1.Sibling = child2;
            child2.Sibling = child1;

            var buf = LuminPackSerializer.SerializeJson(root);
            var deserializedRoot = LuminPackSerializer.DeserializeJson<TreeNode>(buf);

            var deserializedChild1 = deserializedRoot?.FirstChild;
            var deserializedChild2 = deserializedRoot?.FirstChild?.Sibling;

            if (deserializedRoot != null &&
                deserializedChild1 != null &&
                deserializedChild2 != null &&
                ReferenceEquals(deserializedChild1.Sibling, deserializedChild2) &&
                ReferenceEquals(deserializedChild2.Sibling, deserializedChild1) &&
                ReferenceEquals(deserializedChild1.Parent, deserializedRoot) &&
                ReferenceEquals(deserializedChild2.Parent, deserializedRoot))
            {
                output.Add("✓ TestComplexCircleReference [JSON] - PASSED");
            }
            else
            {
                output.Add("✗ TestComplexCircleReference [JSON] - FAILED");
            }
        }
        catch (Exception ex)
        {
            output.Add($"✗ TestComplexCircleReference [JSON] - ERROR: {ex.Message}");
        }
    }

    /// <summary>Verifies test ring circle reference.</summary>
    /// <param name="output">The destination receiving the result.</param>
    public static void TestRingCircleReference(List<string> output)
    {
        try
        {
            var node1 = new TreeNode { Name = "Node1" };
            var node2 = new TreeNode { Name = "Node2" };
            var node3 = new TreeNode { Name = "Node3" };

            node1.Sibling = node2;
            node2.Sibling = node3;
            node3.Sibling = node1;

            var buf = LuminPackSerializer.Serialize(node1);
            var deserializedNode1 = LuminPackSerializer.Deserialize<TreeNode>(buf);

            // Traverse circular references safely.
            if (deserializedNode1?.Sibling?.Sibling?.Sibling != null &&
                ReferenceEquals(deserializedNode1.Sibling.Sibling.Sibling, deserializedNode1) &&
                deserializedNode1.Name == "Node1" &&
                deserializedNode1.Sibling.Name == "Node2" &&
                deserializedNode1.Sibling.Sibling.Name == "Node3")
            {
                output.Add("✓ TestRingCircleReference - PASSED");
            }
            else
            {
                output.Add("✗ TestRingCircleReference - FAILED");
            }
        }
        catch (Exception ex)
        {
            output.Add($"✗ TestRingCircleReference - ERROR: {ex.Message}");
        }

        try
        {
            var node1 = new TreeNode { Name = "Node1" };
            var node2 = new TreeNode { Name = "Node2" };
            var node3 = new TreeNode { Name = "Node3" };

            node1.Sibling = node2;
            node2.Sibling = node3;
            node3.Sibling = node1;

            var buf = LuminPackSerializer.SerializeJson(node1);
            var deserializedNode1 = LuminPackSerializer.DeserializeJson<TreeNode>(buf);

            // Traverse circular references safely.
            if (deserializedNode1?.Sibling?.Sibling?.Sibling != null &&
                ReferenceEquals(deserializedNode1.Sibling.Sibling.Sibling, deserializedNode1) &&
                deserializedNode1.Name == "Node1" &&
                deserializedNode1.Sibling.Name == "Node2" &&
                deserializedNode1.Sibling.Sibling.Name == "Node3")
            {
                output.Add("✓ TestRingCircleReference [JSON] - PASSED");
            }
            else
            {
                output.Add("✗ TestRingCircleReference [JSON] - FAILED");
            }
        }
        catch (Exception ex)
        {
            output.Add($"✗ TestRingCircleReference [JSON] - ERROR: {ex.Message}");
        }
    }

    // Add a simple tree-shaped test case.
    /// <summary>Verifies test simple tree.</summary>
    /// <param name="output">The destination receiving the result.</param>
    public static void TestSimpleTree(List<string> output)
    {
        try
        {
            var root = new TreeNode { Name = "Root" };
            var child = new TreeNode { Name = "Child" };

            root.FirstChild = child;
            child.Parent = root;

            var buf = LuminPackSerializer.Serialize(root);

            var deserializedRoot = LuminPackSerializer.Deserialize<TreeNode>(buf);

            if (deserializedRoot?.FirstChild?.Parent != null &&
                ReferenceEquals(deserializedRoot.FirstChild.Parent, deserializedRoot))
            {
                output.Add("✓ TestSimpleTree - PASSED");
            }
            else
            {
                output.Add("✗ TestSimpleTree - FAILED");
            }
        }
        catch (Exception ex)
        {
            output.Add($"✗ TestSimpleTree - ERROR: {ex.Message}");
        }

        try
        {
            var root = new TreeNode { Name = "Root" };
            var child = new TreeNode { Name = "Child" };

            root.FirstChild = child;
            child.Parent = root;

            var buf = LuminPackSerializer.SerializeJson(root);

            var deserializedRoot = LuminPackSerializer.DeserializeJson<TreeNode>(buf);

            if (deserializedRoot?.FirstChild?.Parent != null &&
                ReferenceEquals(deserializedRoot.FirstChild.Parent, deserializedRoot))
            {
                output.Add("✓ TestSimpleTree [JSON] - PASSED");
            }
            else
            {
                output.Add("✗ TestSimpleTree [JSON] - FAILED");
            }
        }
        catch (Exception ex)
        {
            output.Add($"✗ TestSimpleTree [JSON] - ERROR: {ex.Message}");
        }
    }

    /// <summary>Verifies run all tests.</summary>
    /// <param name="output">The destination receiving the result.</param>
    public static void RunAllTests(List<string> output)
    {

        TestSimpleTree(output);

    }
}
