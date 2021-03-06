﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReflectionComparison.Compare
{
    class Program
    {
        static string RQuotes (string value)
        {
            if (value[0] == '"')
                return value.Substring(1, value.Length - 2);
            else
                return value;
        }
        static void Main(string[] args)
        {
            Console.WriteLine("Bridge result?");
            string bridgeResult = File.ReadAllText(args.Length == 2 ? args[0] : RQuotes(Console.ReadLine()));
            Console.WriteLine(".NET result?");
            string netResult = File.ReadAllText(args.Length == 2 ? args[1] : RQuotes(Console.ReadLine()));
            CSNamespace bridgeNamespace = CSNamespace.FromText(bridgeResult);
            CSNamespace netNamespace = CSNamespace.FromText(netResult);
            bridgeNamespace.SortAll();
            netNamespace.SortAll();
            using (writer = new StreamWriter("result.html"))
            {
                #region Start
                writer.Write(@"<!DOCTYPE html>
<html>
<head>
  <meta charset=""utf-8"">
  <title>Comparison of Bridge and .NET</title>
  <!-- 2 load the theme CSS file -->
  <link rel=""stylesheet"" href=""dist/themes/default/style.min.css"" />
</head>
<body><div id=""main"">");
#endregion
                DiffNamespaces(bridgeNamespace, netNamespace);
#region End
                writer.Write(@"</div>
<!-- 4 include the jQuery library -->
  <script src=""dist/libs/jquery.min.js""></script>
  <script src = ""dist/jstree.min.js"" ></script>
<script>
$(function(){
$('#main').jstree();
});
</script>
</body>
</html>");
#endregion
            }
        }

        static StreamWriter writer;

        static void DiffTypes (CSMemberedType bridgeType, CSMemberedType netType)
        {
            if (bridgeType == null || netType == null)
                return;
            HashSet<string> membersHash = new HashSet<string>();
            Dictionary<string, CSMember> MembersOf(CSMemberedType type)
            {
                string NameOf (CSMember member)
                {
                    CSMethod csMethod;
                    if ((csMethod = member as CSMethod) != null)
                    {
                        return member.Name + "(" + string.Join(", ", csMethod.Parameters.ConvertAll(v2 => v2.Type)) + ")";
                    }
                    else
                        return member.Name;
                }
                var dic = new Dictionary<string, CSMember>();
                foreach (var member in type.Members)
                {
                    var name = NameOf(member);
                    if (!dic.ContainsKey(name))
                    {
                        dic.Add(name, member);
                        membersHash.Add(name);
                    }
                }
                return dic;
            }
            var bridgeMembers = MembersOf(bridgeType);
            var netMembers = MembersOf(netType);
            writer.Write("<ul>");
            foreach (var @string in membersHash.OrderBy(v => v))
            {
                bool inBridge = bridgeMembers.ContainsKey(@string);
                bool inNet = netMembers.ContainsKey(@string);
                var detectedProperty = inNet ? netMembers[@string] : bridgeMembers[@string];
                string icon;
                if (detectedProperty.Attributes.HasFlag(Attributes.Field))
                    icon = "field";
                else if (detectedProperty.Attributes.HasFlag(Attributes.Property))
                    icon = "property";
                else if (detectedProperty.Attributes.HasFlag(Attributes.Method))
                    icon = "method";
                else
                    throw new NotImplementedException();
                writer.Write("<li data-jstree='{\"icon\":\"dist/images/");
                writer.Write(icon);
                writer.Write(".png\"}'><span style=\"color:");
                CompareSupport(inBridge, inNet);
                writer.Write("\">");
                writer.Write(@string);
                writer.Write("</span>");
                if (inNet && inBridge)
                    CompareAttributes(netMembers[@string].Attributes, bridgeMembers[@string].Attributes);
                writer.Write("</li>");
            }
            writer.Write("</ul>");
        }
        
        static void DiffNamespaces (CSNamespace bridgeNamespace, CSNamespace netNamespace)
        {
            writer.Write("<ul>");
            HashSet<string> combinedTypeStrings = new HashSet<string>();
            HashSet<string> combinedNamespaceStrings = new HashSet<string>();
            (Dictionary<string, CSType> classes, Dictionary<string, CSNamespace> namespaces) CreateFrom(CSNamespace @namespace)
            {
                Dictionary<string, CSType> classes = new Dictionary<string, CSType>();
                Dictionary<string, CSNamespace> namespaces = new Dictionary<string, CSNamespace>();
                foreach (var @class in @namespace.NestedClasses)
                {
                    combinedTypeStrings.Add(@class.Name);
                    classes.Add(@class.Name, @class);
                }
                foreach (var nestedNamespace in @namespace.NestedNamespaces)
                {
                    namespaces.Add(nestedNamespace.name, nestedNamespace);
                    combinedNamespaceStrings.Add(nestedNamespace.name);
                }
                return (classes, namespaces);
            }
            (Dictionary<string, CSType> bridgeTypes, Dictionary<string, CSNamespace> bridgeNamespaces) = CreateFrom(bridgeNamespace);
            (Dictionary<string, CSType> netTypes, Dictionary<string, CSNamespace> netNamespaces) = CreateFrom(netNamespace);
            foreach (var @string in combinedNamespaceStrings.OrderBy(v => v))
            {
                bool inBridge = bridgeNamespaces.ContainsKey(@string);
                bool inNet = netNamespaces.ContainsKey(@string);
                writer.Write($"<li data-jstree='{"{"}\"icon\":\"dist/images/namespace.png\"{"}"}'><span style=\"color:");
                CompareSupport(inBridge, inNet);
                writer.Write($"\">{@string}</span>");
                if (inBridge && inNet)
                    DiffNamespaces(bridgeNamespaces[@string], netNamespaces[@string]);
                writer.Write("</li>");
                // TODO: Add to tree.
            }
            foreach (var @string in combinedTypeStrings.OrderBy(v => v))
            {
                bool inBridge = bridgeTypes.ContainsKey(@string);
                bool inNet = netTypes.ContainsKey(@string);
                writer.Write("<li data-jstree='{\"icon\":\"dist/images/class.png\"}'><span style=\"color:");
                CompareSupport(inBridge, inNet);
                writer.Write("\">");
                writer.Write(@string);
                writer.Write("</span>");
                if (inBridge && inNet)
                {
                    var bridgeType = bridgeTypes[@string];
                    var netType = netTypes[@string];
                    CompareAttributes(bridgeType.Attributes, netType.Attributes);
                    DiffTypes(bridgeType as CSMemberedType, netType as CSMemberedType);
                }
                writer.Write("</li>");
                // TODO: Add to tree.
            }
            writer.Write("</ul>");
        }

        public static void CompareAttributes (Attributes bridgeAttributes, Attributes netAttributes)
        {
            if (netAttributes != bridgeAttributes)
                writer.Write($@"<font color=""purple"" title=""Bridge -> {bridgeAttributes} .NET -> {netAttributes}"">*</font>");
        }

        public static void CompareSupport (bool inBridge, bool inNet)
        {
            string color;
            if (inBridge && inNet)
                color = "black";
            else if (inBridge)
                color = "blue";
            else if (inNet)
                color = "red";
            else
                throw new Exception();
            writer.Write(color);
        }
    }
}
