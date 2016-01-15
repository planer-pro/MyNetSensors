﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using MyNetSensors.Gateways;
using LiteGraph;
using MyNetSensors.LogicalNodes;
using MyNetSensors.LogicalNodesMySensors;
using MyNetSensors.LogicalNodesUI;
using MyNetSensors.SerialControllers;
using Newtonsoft.Json;
using Input = LiteGraph.Input;
using Node = MyNetSensors.Gateways.Node;
using Output = LiteGraph.Output;

namespace MyNetSensors.WebController.Controllers
{
    public class NodesEditorAPIController : Controller
    {
        const string MAIN_PANEL_ID = "Main";

        private LogicalNodesEngine engine = SerialController.logicalNodesEngine;

        public List<LiteGraph.Node> GetNodes(string panelId)
        {
            if (engine == null)
                return null;

            if (panelId == null)
                panelId = MAIN_PANEL_ID;

            List<LogicalNode> nodes = engine.nodes;
            if (nodes == null || !nodes.Any())
                return null;

            return (
                from node in nodes
                where node.PanelId == panelId
                select ConvertLogicalNodeToLitegraphNode(node)).ToList();
        }


        public LiteGraph.Node ConvertLogicalNodeToLitegraphNode(LogicalNode logicalNode)
        {
            LiteGraph.Node node = new LiteGraph.Node
            {
                title = logicalNode.Title,
                type = logicalNode.Type,
                id = logicalNode.Id,
                panel_id = logicalNode.PanelId
            };


            node.properties["objectType"] = logicalNode.GetType().ToString();

            if (logicalNode.Position != null)
                node.pos = new[] { logicalNode.Position.X, logicalNode.Position.Y };

            if (logicalNode.Size != null)
                node.size = new[] { logicalNode.Size.Width, logicalNode.Size.Height };

            node.inputs = new List<Input>();
            node.outputs = new List<Output>();

            if (logicalNode.Inputs != null)
                foreach (var input in logicalNode.Inputs)
                {
                    node.inputs.Add(new Input
                    {
                        name = input.Name,
                        type = "string",
                        link = engine.GetLinkForInput(input)?.Id
                    });
                }


            if (logicalNode.Outputs != null)
                foreach (var output in logicalNode.Outputs)
                {
                    List<LogicalLink> links = engine.GetLinksForOutput(output);
                    if (links != null)
                    {
                        string[] linksIds = new string[links.Count];
                        for (int i = 0; i < links.Count; i++)
                        {
                            linksIds[i] = links[i].Id;
                        }
                        node.outputs.Add(new Output
                        {
                            name = output.Name,
                            type = "string",
                            links = linksIds
                        });
                    }
                    else
                    {
                        node.outputs.Add(new Output
                        {
                            name = output.Name,
                            type = "string"
                        });
                    }
                }

            if (logicalNode is LogicalNodeUI)
            {
                LogicalNodeUI n = (LogicalNodeUI)logicalNode;
                node.properties["name"] = n.Name;
            }

            if (logicalNode is LogicalNodeUISlider)
            {
                LogicalNodeUISlider n = (LogicalNodeUISlider) logicalNode;
                node.properties["min"] = n.Min.ToString();
                node.properties["max"] = n.Max.ToString();
            }

            if (logicalNode is LogicalNodeConstant)
            {
                LogicalNodeConstant n = (LogicalNodeConstant)logicalNode;
                node.properties["value"] = n.Value;
            }

            if (logicalNode is LogicalNodePanel)
            {
                LogicalNodePanel n = (LogicalNodePanel)logicalNode;
                node.properties["panelname"] = n.Name;
            }

            if (logicalNode is LogicalNodePanelInput)
            {
                LogicalNodePanelInput n = (LogicalNodePanelInput)logicalNode;
                node.properties["name"] = n.Name;
            }

            if (logicalNode is LogicalNodePanelOutput)
            {
                LogicalNodePanelOutput n = (LogicalNodePanelOutput)logicalNode;
                node.properties["name"] = n.Name;
            }

            return node;
        }


        public Link ConvertLogicalNodeToLitegraphLink(LogicalLink logicalLink)
        {
            if (logicalLink == null)
                return null;
            LiteGraph.Link link = new LiteGraph.Link
            {
                origin_id = engine.GetOutputOwner(logicalLink.OutputId).Id,
                target_id = engine.GetInputOwner(logicalLink.InputId).Id,
                origin_slot = GetOutputSlot(logicalLink.OutputId),
                target_slot = GetInputSlot(logicalLink.InputId),
                id = logicalLink.Id,
                panel_id = logicalLink.PanelId
            };

            return link;
        }


        public List<LiteGraph.Link> GetLinks(string panelId)
        {
            if (engine == null)
                return null;

            List<LogicalLink> links = engine.links;
            if (links == null || !links.Any())
                return null;

            return (
                from link in links
                where link.PanelId == panelId
                select ConvertLogicalNodeToLitegraphLink(link)).ToList();
        }


        private int GetInputSlot(string inputId)
        {
            for (int i = 0; i < engine.nodes.Count; i++)
            {
                for (int j = 0; j < engine.nodes[i].Inputs.Count; j++)
                {
                    if (engine.nodes[i].Inputs[j].Id == inputId)
                        return j;
                }
            }
            return -1;
        }

        private int GetOutputSlot(string outputId)
        {
            for (int i = 0; i < engine.nodes.Count; i++)
            {
                for (int j = 0; j < engine.nodes[i].Outputs.Count; j++)
                {
                    if (engine.nodes[i].Outputs[j].Id == outputId)
                        return j;
                }
            }
            return -1;
        }



        public bool RemoveLink(Link link)
        {
            if (engine == null)
                return false;

            if (link.origin_id == null || link.target_id == null)
                return false;

            LogicalNode outNode = SerialController.logicalNodesEngine.GetNode(link.origin_id);
            LogicalNode inNode = SerialController.logicalNodesEngine.GetNode(link.target_id);
            if (outNode == null || inNode == null)
            {
                engine.LogEngineError($"Can`t remove link from [{link.origin_id}] to [{link.target_id}]. Does not exist.");
                return false;
            }
            engine.RemoveLink(outNode.Outputs[link.origin_slot], inNode.Inputs[link.target_slot]);

            return true;
        }

        public bool CreateLink(Link link)
        {
            if (engine == null)
                return false;

            LogicalNode outNode = SerialController.logicalNodesEngine.GetNode(link.origin_id);
            LogicalNode inNode = SerialController.logicalNodesEngine.GetNode(link.target_id);

            if (outNode == null || inNode == null)
            {
                engine.LogEngineError($"Can`t create link from [{link.origin_id}] to [{link.target_id}]. Does not exist.");
                return false;
            }

            engine.AddLink(outNode.Outputs[link.origin_slot], inNode.Inputs[link.target_slot]);
            return true;
        }

        public bool CreateNode(LiteGraph.Node node)
        {
            if (engine == null)
                return false;

            LogicalNode newNode;

            try
            {
                string type = node.properties["objectType"];
                string assemblyName = type.Split('.')[1];

                var newObject = Activator.CreateInstance(assemblyName, type);
                newNode = (LogicalNode) newObject.Unwrap();
            }
            catch
            {
                engine.LogEngineError($"Can`t create node [{node.properties["objectType"]}]. Type does not exist.");
                return false;
            }

            //LogicalNode newNode = newObject as LogicalNode;
            newNode.Position = new Position { X = node.pos[0], Y = node.pos[1] };
            if (node.size.Length==2)
                newNode.Size = new Size { Width = node.size[0], Height = node.size[1] };
            newNode.Id = node.id;
            newNode.PanelId = node.panel_id ?? MAIN_PANEL_ID;

            engine.AddNode(newNode);

            return true;
        }

        public bool RemoveNode(LiteGraph.Node node)
        {
            if (engine == null)
                return false;

            LogicalNode oldNode = engine.GetNode(node.id);
            if (oldNode == null)
            {
                engine.LogEngineError($"Can`t remove node [{node.id}]. Does not exist.");
                return false;
            }

            engine.RemoveNode(oldNode);
            return true;
        }

        public bool UpdateNode(LiteGraph.Node node)
        {
            if (engine == null)
                return false;

            LogicalNode oldNode = engine.GetNode(node.id);
            if (oldNode == null)
            {
                engine.LogEngineError($"Can`t update node [{node.id}]. Does not exist.");
                return false;
            }

            oldNode.Position = new Position { X = node.pos[0], Y = node.pos[1] };
            oldNode.Size = new Size { Width = node.size[0], Height = node.size[1] };

            engine.UpdateNode(oldNode);

            return true;
        }


        public bool PanelSettings(string id, string panelname)
        {
            LogicalNode n = engine.GetNode(id);
            if (n == null)
            {
                engine.LogEngineError($"Can`t set settings for Panel [{id}]. Does not exist.");
                return false;
            }

            LogicalNodePanel node = (LogicalNodePanel)n;
            node.Name = panelname;
            engine.UpdateNode(node);

            return true;
        }

        public bool InputOutputSettings(string id, string name)
        {
            LogicalNode n = engine.GetNode(id);
            if (n == null)
            {
                engine.LogEngineError($"Can`t set settings for Input/Output [{id}]. Does not exist.");
                return false;
            }

            if (n is LogicalNodePanelInput)
            {
                LogicalNodePanelInput node = (LogicalNodePanelInput)n;
                node.Name = name;
                engine.UpdateNode(node);
            }

            if (n is LogicalNodePanelOutput)
            {
                LogicalNodePanelOutput node = (LogicalNodePanelOutput)n;
                node.Name = name;
                engine.UpdateNode(node);
            }

            return true;
        }

        public bool UINodeSettings(string id, string name)
        {
            LogicalNode n = engine.GetNode(id);
            if (n == null)
            {
                engine.LogEngineError($"Can`t set settings for Node [{id}]. Does not exist.");
                return false;
            }

            LogicalNodeUI node = (LogicalNodeUI)n;
            node.Name = name;
            engine.UpdateNode(node);

            return true;
        }


        public bool UISliderSettings(string id, string name, int min,int max)
        {
            LogicalNode n = engine.GetNode(id);
            if (n == null)
            {
                engine.LogEngineError($"Can`t set settings for Node [{id}]. Does not exist.");
                return false;
            }
            if (min >= max)
            {
                engine.LogEngineError($"Can`t set settings for Node [{id}]. Min must be > Max.");
                return false;
            }

            LogicalNodeUISlider node = (LogicalNodeUISlider)n;
            node.Name = name;
            node.Min = min;
            node.Max = max;
            engine.UpdateNode(node);

            return true;
        }

        public bool ConstantSettings(string id, string value)
        {
            LogicalNode n = engine.GetNode(id);
            if (n == null)
            {
                engine.LogEngineError($"Can`t set settings for Node [{id}]. Does not exist.");
                return false;
            }

            LogicalNodeConstant node = (LogicalNodeConstant)n;
            node.SetValue(value);
            engine.UpdateNode(node);

            return true;
        }


        //private int CalculateNodeHeight(LiteGraph.Node node)
        //{
        //const int SLOT_SIZE = 15;
        //const int NODE_WIDTH = 150;

        //    int sizeOutY = 0, sizeInY = 0;

        //    if (node.outputs != null)
        //        sizeOutY = SLOT_SIZE + (SLOT_SIZE * node.outputs.Count);
        //    if (node.inputs != null)
        //        sizeInY = SLOT_SIZE + (SLOT_SIZE * node.inputs.Count);

        //    return (sizeOutY > sizeInY) ? sizeOutY : sizeInY;
        //}



        //private void MooveNewNodesToFreeSpace(List<LiteGraph.Node> nodes)
        //{
        //const int SLOT_SIZE = 15;
        //const int NODE_WIDTH = 150;

        //    const int START_POS = 50;
        //    const int FREE_SPACE_UNDER = 30;

        //    for (int k = 0; k < nodes.Count; k++)
        //    {
        //        if (nodes[k].pos != null)
        //            continue;

        //        nodes[k].pos = new float[2];

        //        float result = START_POS;


        //        for (int i = 0; i < nodes.Count; i++)
        //        {
        //            float needFromY = result;
        //            float needToY = result + nodes[k].size[1];

        //            if (i == k)
        //                continue;

        //            if (nodes[i].pos == null)
        //                continue;

        //            if (nodes[i].pos[0] > NODE_WIDTH + 20 + START_POS)
        //                continue;

        //            float occupyFromY = nodes[i].pos[1]- FREE_SPACE_UNDER;
        //            float occupyToY = nodes[i].pos[1] + nodes[i].size[1];

        //            if (occupyFromY <= needToY && occupyToY >= needFromY)
        //            {
        //                result = occupyToY + FREE_SPACE_UNDER;
        //                i = -1;
        //            }
        //        }

        //        nodes[k].pos[0] = START_POS;
        //        nodes[k].pos[1] = result;
        //    }
        //}

        //public IActionResult GetGraph()
        //{
        //    string json = "{ \"iteration\":0,\"last_node_id\":3,\"last_link_id\":2,\"links\":{ \"0\":{ \"id\":0,\"origin_id\":2,\"origin_slot\":0,\"target_id\":0,\"target_slot\":0,\"data\":null},\"1\":{ \"id\":1,\"origin_id\":0,\"origin_slot\":0,\"target_id\":1,\"target_slot\":0,\"data\":null} },\"config\":{ },\"nodes\":[{\"id\":0,\"title\":\"SimpleNode\",\"type\":\"Nodes/SimpleNode\",\"pos\":[344,234],\"size\":[100,20],\"flags\":{},\"inputs\":[{\"name\":\"in\",\"type\":\"number\",\"link\":0}],\"outputs\":[{\"name\":\"out\",\"type\":\"number\",\"links\":[1]}],\"properties\":{\"min\":0,\"max\":1}},{\"id\":2,\"title\":\"SimpleOut\",\"type\":\"Nodes/SimpleOut\",\"pos\":[211,282],\"size\":[100,20],\"flags\":{},\"outputs\":[{\"name\":\"out\",\"type\":\"number\",\"links\":[0]}],\"properties\":{\"min\":0,\"max\":1}},{\"id\":1,\"title\":\"SimpleIn\",\"type\":\"Nodes/SimpleIn\",\"pos\":[471,160],\"size\":[100,20],\"flags\":{},\"inputs\":[{\"name\":\"in\",\"type\":\"number\",\"link\":1}],\"properties\":{\"min\":0,\"max\":1}}]}";
        //    Graph graph = JsonConvert.DeserializeObject<Graph>(json);


        //    return Json(graph);
        //}

        public bool PutGraph(string json)
        {
            return false;
            Graph graph = JsonConvert.DeserializeObject<Graph>(json);

            engine.RemoveAllLinks();
            //hardwareEngine.RemoveAllNonHardwareNodes();

            foreach (var node in graph.nodes)
            {
                string type = node.properties["objectType"];

                if (type == "MyNetSensors.LogicalNodes.LogicalHardwareNode")
                {
                    LogicalNode oldNode = engine.GetNode(node.id);
                    oldNode.Position = new Position { X = node.pos[0], Y = node.pos[1] };
                }
                else
                {
                    CreateNode(node);
                }
            }

            foreach (Link link in graph.links.Values)
            {
                CreateLink(link);
            }

            //for (int i = 0; i < graph.links.Count; i++)
            //{
            //    Link link = graph.links[i];

            //    LogicalNode outNode = SerialController.logicalNodesEngine.GetNode(link.origin_id);
            //    LogicalNode inNode = SerialController.logicalNodesEngine.GetNode(link.target_id);
            //    engine.AddLink(outNode.Outputs[link.origin_slot], inNode.Inputs[link.target_slot]);
            //}


            return true;
        }
    }
}
