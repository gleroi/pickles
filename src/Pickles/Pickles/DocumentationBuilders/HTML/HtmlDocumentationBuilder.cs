﻿#region License

/*
    Copyright [2011] [Jeffrey Cameron]

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/

#endregion

using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using System.Collections.Generic;
using NGenerics.DataStructures.Trees;
using NGenerics.Patterns.Visitor;
using PicklesDoc.Pickles.DirectoryCrawler;
using PicklesDoc.Pickles.Extensions;
using log4net;

namespace PicklesDoc.Pickles.DocumentationBuilders.HTML
{
    public class HtmlDocumentationBuilder : IDocumentationBuilder
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Configuration configuration;
        private readonly HtmlDocumentFormatter htmlDocumentFormatter;
        private readonly HtmlResourceWriter htmlResourceWriter;

        public HtmlDocumentationBuilder(Configuration configuration,
                                        HtmlDocumentFormatter htmlDocumentFormatter,
                                        HtmlResourceWriter htmlResourceWriter)
        {
            this.configuration = configuration;
            this.htmlDocumentFormatter = htmlDocumentFormatter;
            this.htmlResourceWriter = htmlResourceWriter;
        }

        #region IDocumentationBuilder Members

        Dictionary<string, FeatureDirectoryTreeNode> featurePaths = new Dictionary<string, FeatureDirectoryTreeNode>();

        public void Build(GeneralTree<IDirectoryTreeNode> features)
        {
            if (log.IsInfoEnabled)
            {
                log.InfoFormat("Writing HTML to {0}", this.configuration.OutputFolder.FullName);
            }

            this.htmlResourceWriter.WriteTo(this.configuration.OutputFolder.FullName);


            var featureVisitor = new ActionVisitor<IDirectoryTreeNode>(node =>
            {
                FeatureDirectoryTreeNode featureNode = node as FeatureDirectoryTreeNode;
                if (featureNode != null)
                {
                    if (!featurePaths.ContainsKey(featureNode.Feature.Name))
                    {
                        featurePaths.Add(featureNode.Feature.Name, featureNode);
                    }
                }
            });

            var actionVisitor = new ActionVisitor<IDirectoryTreeNode>(node =>
                                                                          {
                                                                              if (node.IsIndexMarkDownNode())
                                                                              {
                                                                                  return;
                                                                              }

                                                                              string nodePath =
                                                                                  Path.Combine(
                                                                                      this.configuration.OutputFolder.
                                                                                          FullName,
                                                                                      node.RelativePathFromRoot);
                                                                              string htmlFilePath;

                                                                              if (node.IsContent)
                                                                              {
                                                                                  htmlFilePath =
                                                                                      nodePath.Replace(
                                                                                          Path.GetExtension(nodePath),
                                                                                          ".html");
                                                                              }
                                                                              else
                                                                              {
                                                                                  Directory.CreateDirectory(nodePath);

                                                                                  htmlFilePath = Path.Combine(nodePath,
                                                                                                              "index.html");
                                                                              }

                                                                              using (
                                                                                  var writer =
                                                                                      new StreamWriter(htmlFilePath,
                                                                                                       false,
                                                                                                       Encoding.UTF8))
                                                                              {
                                                                                  XDocument document =
                                                                                      this.htmlDocumentFormatter.Format(
                                                                                          node, features,
                                                                                          this.configuration.FeatureFolder);
                                                                                  CompleteFeatureLinks(document, node.OriginalLocationUrl);
                                                                                  document.Save(writer);
                                                                                  writer.Close();
                                                                              }
                                                                          });
            if (features != null) {
                features.AcceptVisitor(featureVisitor);
                features.AcceptVisitor(actionVisitor);
            }
        }

        private void CompleteFeatureLinks(XDocument doc, Uri currentDir)
        {
            var links = doc.Descendants(XName.Get("a", HtmlNamespace.Xhtml.NamespaceName));
            foreach (XElement link in links)
            {
                XAttribute href = link.Attribute(XName.Get("href"));
                if (String.IsNullOrEmpty(href.Value))
                {
                    string featureName = link.Value;
                    FeatureDirectoryTreeNode node;
                    if (featurePaths.TryGetValue(featureName, out node))
                    {
                        href.Value = node.GetRelativeUriTo(currentDir);
                    }
                }
            }
        }

        #endregion
    }
}