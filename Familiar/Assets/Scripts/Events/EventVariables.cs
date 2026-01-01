using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static storage for event-related variables that can be set during dialogue
/// and read by the event system to determine branching behavior.
///
/// Usage in Ink:
///   ~ SetEventVar("allowed_inside", true)
///
/// Usage in WaypointData:
///   Branch Variable: allowed_inside
///   Branch Value: true
///   Branch To Waypoint: InsideWaypoint
/// </summary>
public static class EventVariables
{
    private static Dictionary<string, object> variables = new Dictionary<string, object>();

    /// <summary>
    /// Sets a variable value. Called from Ink via external function.
    /// </summary>
    public static void SetVariable(string name, object value)
    {
        variables[name] = value;
        Debug.Log($"EventVariables: Set '{name}' = {value}");
    }

    /// <summary>
    /// Gets a variable value as a specific type.
    /// </summary>
    public static T GetVariable<T>(string name, T defaultValue = default)
    {
        if (variables.TryGetValue(name, out object value))
        {
            try
            {
                // Handle Ink's type system (it uses int for bools sometimes)
                if (typeof(T) == typeof(bool) && value is int intVal)
                {
                    return (T)(object)(intVal != 0);
                }
                return (T)System.Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }
        return defaultValue;
    }

    /// <summary>
    /// Checks if a variable exists.
    /// </summary>
    public static bool HasVariable(string name)
    {
        return variables.ContainsKey(name);
    }

    /// <summary>
    /// Checks if a variable equals a specific string value.
    /// Handles type conversion for comparison.
    /// </summary>
    public static bool CheckVariable(string name, string expectedValue)
    {
        if (!variables.TryGetValue(name, out object value))
        {
            return false;
        }

        string actualValue = value?.ToString()?.ToLower() ?? "";
        string expected = expectedValue?.ToLower() ?? "";

        // Handle boolean comparisons
        if (expected == "true" || expected == "false")
        {
            bool expectedBool = expected == "true";

            if (value is bool boolVal)
                return boolVal == expectedBool;
            if (value is int intVal)
                return (intVal != 0) == expectedBool;
            if (value is string strVal)
                return strVal.ToLower() == expected;
        }

        return actualValue == expected;
    }

    /// <summary>
    /// Clears all variables. Call this when starting a new game or resetting state.
    /// </summary>
    public static void ClearAll()
    {
        variables.Clear();
        Debug.Log("EventVariables: Cleared all variables");
    }

    /// <summary>
    /// Clears a specific variable.
    /// </summary>
    public static void ClearVariable(string name)
    {
        if (variables.Remove(name))
        {
            Debug.Log($"EventVariables: Cleared '{name}'");
        }
    }
}
