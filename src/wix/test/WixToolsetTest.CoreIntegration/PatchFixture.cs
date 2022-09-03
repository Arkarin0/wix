// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace WixToolsetTest.CoreIntegration
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Xml;
    using System.Xml.Linq;
    using Example.Extension;
    using WixBuildTools.TestSupport;
    using WixToolset.Core.TestPackage;
    using WixToolset.Data;
    using WixToolset.Data.Burn;
    using Xunit;

    public class PatchFixture
    {
        private static readonly XNamespace PatchNamespace = "http://www.microsoft.com/msi/patch_applicability.xsd";

        [Fact]
        public void CanBuildSimplePatch()
        {
            var folder = TestData.Get(@"TestData", "PatchSingle");

            using (var fs = new DisposableFileSystem())
            {
                var tempFolder = fs.GetFolder();

                var baselinePdb = BuildMsi("Baseline.msi", folder, tempFolder, "1.0.0", "1.0.0", "1.0.0");
                var update1Pdb = BuildMsi("Update.msi", folder, tempFolder, "1.0.1", "1.0.1", "1.0.1");
                var patchPdb = BuildMsp("Patch1.msp", folder, tempFolder, "1.0.1");
                var patchPath = Path.ChangeExtension(patchPdb, ".msp");

                Assert.True(File.Exists(baselinePdb));
                Assert.True(File.Exists(update1Pdb));

                var doc = GetExtractPatchXml(patchPath);
                WixAssert.StringEqual("{7D326855-E790-4A94-8611-5351F8321FCA}", doc.Root.Element(PatchNamespace + "TargetProductCode").Value);

                var names = Query.GetSubStorageNames(patchPath);
                WixAssert.CompareLineByLine(new[] { "#RTM.1", "RTM.1" }, names);

                var cab = Path.Combine(tempFolder, "foo.cab");
                Query.ExtractStream(patchPath, "foo.cab", cab);
                Assert.True(File.Exists(cab));

                var files = Query.GetCabinetFiles(cab);
                WixAssert.CompareLineByLine(new[] { "a.txt", "b.txt" }, files.Select(f => f.Name).ToArray());
            }
        }

        [Fact]
        public void CanBuildSimplePatchWithFileChanges()
        {
            var folder = TestData.Get(@"TestData", "PatchWithFileChanges");

            using (var fs = new DisposableFileSystem())
            {
                var baseFolder = fs.GetFolder();
                var tempFolderBaseline = Path.Combine(baseFolder, "baseline");
                var tempFolderUpdate = Path.Combine(baseFolder, "update");
                var tempFolderPatch = Path.Combine(baseFolder, "patch");

                var baselinePdb = BuildMsi("Baseline.msi", folder, tempFolderBaseline, "1.0.0", "1.0.0", "1.0.0", new[] { Path.Combine(folder, ".baseline-data") });
                var update1Pdb = BuildMsi("Update.msi", folder, tempFolderUpdate, "1.0.1", "1.0.1", "1.0.1", new[] { Path.Combine(folder, ".update-data") });
                var patchPdb = BuildMsp("Patch1.msp", folder, tempFolderPatch, "1.0.1", bindpaths: new[] { Path.GetDirectoryName(baselinePdb), Path.GetDirectoryName(update1Pdb) });
                var patchPath = Path.ChangeExtension(patchPdb, ".msp");

                var doc = GetExtractPatchXml(patchPath);
                WixAssert.StringEqual("{7D326855-E790-4A94-8611-5351F8321FCA}", doc.Root.Element(PatchNamespace + "TargetProductCode").Value);

                var names = Query.GetSubStorageNames(patchPath);
                WixAssert.CompareLineByLine(new[] { "#RTM.1", "RTM.1" }, names);

                var cab = Path.Combine(baseFolder, "foo.cab");
                Query.ExtractStream(patchPath, "foo.cab", cab);
                Assert.True(File.Exists(cab));

                var files = Query.GetCabinetFiles(cab);
                var file = files.Single();
                WixAssert.StringEqual("a.txt", file.Name);
                var contents = file.OpenText().ReadToEnd();
                WixAssert.StringEqual("This is A v1.0.1\r\n", contents);
            }
        }

        [Fact]
        public void CanBuildSimplePatchWithNoFileChanges()
        {
            var folder = TestData.Get(@"TestData", "PatchNoFileChanges");

            using (var fs = new DisposableFileSystem())
            {
                var tempFolder = fs.GetFolder();

                var baselinePdb = BuildMsi("Baseline.msi", folder, tempFolder, "1.0.0", "1.0.0", "1.0.0");
                var update1Pdb = BuildMsi("Update.msi", folder, tempFolder, "1.0.1", "1.0.1", "1.0.1");
                var patchPdb = BuildMsp("Patch1.msp", folder, tempFolder, "1.0.1", hasNoFiles: true);
                var patchPath = Path.ChangeExtension(patchPdb, ".msp");

                Assert.True(File.Exists(baselinePdb));
                Assert.True(File.Exists(update1Pdb));

                var doc = GetExtractPatchXml(patchPath);
                WixAssert.StringEqual("{7D326855-E790-4A94-8611-5351F8321FCA}", doc.Root.Element(PatchNamespace + "TargetProductCode").Value);

                var names = Query.GetSubStorageNames(patchPath);
                WixAssert.CompareLineByLine(new[] { "#RTM.1", "RTM.1" }, names);

                var cab = Path.Combine(tempFolder, "foo.cab");
                Query.ExtractStream(patchPath, "foo.cab", cab);
                Assert.True(File.Exists(cab));

                var files = Query.GetCabinetFiles(cab);
                Assert.Empty(files);
            }
        }

        [Fact]
        public void CanBuildSimplePatchWithSoftwareTag()
        {
            var folder = TestData.Get(@"TestData", "PatchWithSoftwareTag");

            using (var fs = new DisposableFileSystem())
            {
                var baseFolder = fs.GetFolder();
                var tempFolderBaseline = Path.Combine(baseFolder, "baseline");
                var tempFolderUpdate = Path.Combine(baseFolder, "update");
                var tempFolderPatch = Path.Combine(baseFolder, "patch");

                var baselinePdb = BuildMsi("Baseline.msi", folder, tempFolderBaseline, "1.0.0", "1.0.0", "1.0.0");
                var update1Pdb = BuildMsi("Update.msi", folder, tempFolderUpdate, "1.0.1", "1.0.1", "1.0.1");
                var patchPdb = BuildMsp("Patch1.msp", folder, tempFolderPatch, "1.0.1", bindpaths: new[] { Path.GetDirectoryName(baselinePdb), Path.GetDirectoryName(update1Pdb) });
                var patchPath = Path.ChangeExtension(patchPdb, ".msp");

                var doc = GetExtractPatchXml(patchPath);
                WixAssert.StringEqual("{7D326855-E790-4A94-8611-5351F8321FCA}", doc.Root.Element(PatchNamespace + "TargetProductCode").Value);

                var names = Query.GetSubStorageNames(patchPath);
                WixAssert.CompareLineByLine(new[] { "#RTM.1", "RTM.1" }, names);

                var cab = Path.Combine(baseFolder, "foo.cab");
                Query.ExtractStream(patchPath, "foo.cab", cab);
                Assert.True(File.Exists(cab));

                var files = Query.GetCabinetFiles(cab);
                var file = files.Single();
                WixAssert.StringEqual("tag1jwIT_7lT286E4Dyji95s65UuO4", file.Name);

                var contents = file.OpenText().ReadToEnd();
                contents = Regex.Replace(contents, @"msi\:package/[A-Z0-9\-]+", "msi:package/G-U-I-D");
                WixAssert.StringEqual(String.Join(Environment.NewLine, new[]
                {
                    "<?xml version='1.0' encoding='utf-8'?>",
                    "<SoftwareIdentity tagId='msi:package/G-U-I-D' name='~Test Package' version='1.0.1' versionScheme='multipartnumeric' xmlns='http://standards.iso.org/iso/19770/-2/2015/schema.xsd'>",
                    "  <Entity name='Example Corporation' regid='regid.1995-08.com.example' role='softwareCreator tagCreator' />",
                    "  <Meta persistentId='msi:upgrade/7D326855-E790-4A94-8611-5351F8321FCA' />",
                    "</SoftwareIdentity>",
                }), contents.Replace('"', '\''));
            }
        }

        [Fact]
        public void CanBuildSimplePatchWithBaselineIdTooLong()
        {
            var folder = TestData.Get(@"TestData", "PatchBaselineIdTooLong");

            using (var fs = new DisposableFileSystem())
            {
                var baseFolder = fs.GetFolder();
                var tempFolderBaseline = Path.Combine(baseFolder, "baseline");
                var tempFolderUpdate = Path.Combine(baseFolder, "update");
                var tempFolderPatch = Path.Combine(baseFolder, "patch");

                var baselinePdb = BuildMsi("Baseline.msi", folder, tempFolderBaseline, "1.0.0", "1.0.0", "1.0.0");
                var update1Pdb = BuildMsi("Update.msi", folder, tempFolderUpdate, "1.0.1", "1.0.1", "1.0.1");
                var patchPdb = BuildMsp("Patch1.msp", folder, tempFolderPatch, "1.0.1", bindpaths: new[] { Path.GetDirectoryName(baselinePdb), Path.GetDirectoryName(update1Pdb) }, hasNoFiles: true, warningsAsErrors: false);
                var patchPath = Path.ChangeExtension(patchPdb, ".msp");

                var doc = GetExtractPatchXml(patchPath);
                WixAssert.StringEqual("{11111111-2222-3333-4444-555555555555}", doc.Root.Element(PatchNamespace + "TargetProductCode").Value);

                var names = Query.GetSubStorageNames(patchPath);
                WixAssert.CompareLineByLine(new[] { "#ThisBaseLineIdIsTooLongAndGe.1", "ThisBaseLineIdIsTooLongAndGe.1" }, names);
            }
        }

        [Fact]
        public void CanBuildPatchFromProductWithFilesFromWixlib()
        {
            var sourceFolder = TestData.Get(@"TestData", "PatchFromWixlib");

            using (var fs = new DisposableFileSystem())
            {
                var baseFolder = fs.GetFolder();
                var tempFolderBaseline = Path.Combine(baseFolder, "baseline");
                var tempFolderUpdate = Path.Combine(baseFolder, "update");
                var tempFolderPatch = Path.Combine(baseFolder, "patch");

                var baselinePdb = BuildMsi("Baseline.msi", sourceFolder, tempFolderBaseline, "1.0.0", "1.0.0", "1.0.0");
                var update1Pdb = BuildMsi("Update.msi", sourceFolder, tempFolderUpdate, "1.0.1", "1.0.1", "1.0.1");
                var patchPdb = BuildMsp("Patch1.msp", sourceFolder, tempFolderPatch, "1.0.1", bindpaths: new[] { Path.GetDirectoryName(baselinePdb), Path.GetDirectoryName(update1Pdb) }, hasNoFiles: true);
                var patchPath = Path.ChangeExtension(patchPdb, ".msp");

                Assert.True(File.Exists(baselinePdb));
                Assert.True(File.Exists(update1Pdb));
            }
        }

        [Fact]
        public void CanBuildBundleWithNonSpecificPatches()
        {
            var folder = TestData.Get(@"TestData", "PatchNonSpecific");

            using (var fs = new DisposableFileSystem())
            {
                var tempFolder = fs.GetFolder();

                var baselinePdb = BuildMsi("Baseline.msi", Path.Combine(folder, "PackageA"), tempFolder, "1.0.0", "A", "B");
                var updatePdb = BuildMsi("Update.msi", Path.Combine(folder, "PackageA"), tempFolder, "1.0.1", "A", "B");
                var patchAPdb = BuildMsp("PatchA.msp", Path.Combine(folder, "PatchA"), tempFolder, "1.0.1", hasNoFiles: true);
                var patchBPdb = BuildMsp("PatchB.msp", Path.Combine(folder, "PatchB"), tempFolder, "1.0.1", hasNoFiles: true);
                var patchCPdb = BuildMsp("PatchC.msp", Path.Combine(folder, "PatchC"), tempFolder, "1.0.1", hasNoFiles: true);
                var bundleAPdb = BuildBundle("BundleA.exe", Path.Combine(folder, "BundleA"), tempFolder);
                var bundleBPdb = BuildBundle("BundleB.exe", Path.Combine(folder, "BundleB"), tempFolder);
                var bundleCPdb = BuildBundle("BundleC.exe", Path.Combine(folder, "BundleC"), tempFolder);

                VerifyPatchTargetCodesInBurnManifest(bundleAPdb, new[]
                {
                    "<PatchTargetCode TargetCode='{26309973-0A5E-4979-B142-98A6E064EDC0}' Product='yes' />",
                });
                VerifyPatchTargetCodesInBurnManifest(bundleBPdb, new[]
                {
                    "<PatchTargetCode TargetCode='{26309973-0A5E-4979-B142-98A6E064EDC0}' Product='yes' />",
                    "<PatchTargetCode TargetCode='{32B0396A-CE36-4570-B16E-F88FA42DC409}' Product='no' />",
                });
                VerifyPatchTargetCodesInBurnManifest(bundleCPdb, new string[0]);
            }
        }

        [Fact]
        public void CanBuildBundleWithSlipstreamPatch()
        {
            var folder = TestData.Get(@"TestData", "PatchSingle");

            using (var fs = new DisposableFileSystem())
            {
                var tempFolder = fs.GetFolder();

                var baselinePdb = BuildMsi("Baseline.msi", folder, tempFolder, "1.0.0", "1.0.0", "1.0.0");
                var update1Pdb = BuildMsi("Update.msi", folder, tempFolder, "1.0.1", "1.0.1", "1.0.1");
                var patchPdb = BuildMsp("Patch1.msp", folder, tempFolder, "1.0.1");
                var bundleAPdb = BuildBundle("BundleA.exe", Path.Combine(folder, "BundleA"), tempFolder);

                using (var wixOutput = WixOutput.Read(bundleAPdb))
                {
                    var manifestData = wixOutput.GetData(BurnConstants.BurnManifestWixOutputStreamName);
                    var doc = new XmlDocument();
                    doc.LoadXml(manifestData);
                    var nsmgr = BundleExtractor.GetBurnNamespaceManager(doc, "w");
                    var slipstreamMspNodes = doc.SelectNodes("/w:BurnManifest/w:Chain/w:MsiPackage/w:SlipstreamMsp", nsmgr).GetTestXmlLines();
                    WixAssert.CompareLineByLine(new[]
                    {
                        "<SlipstreamMsp Id='PatchA' />",
                    }, slipstreamMspNodes);
                }
            }
        }

        private static string BuildMsi(string outputName, string sourceFolder, string baseFolder, string defineV, string defineA, string defineB, IEnumerable<string> bindpaths = null)
        {
            var extensionPath = Path.GetFullPath(new Uri(typeof(ExampleExtensionFactory).Assembly.CodeBase).LocalPath);
            var outputPath = Path.Combine(baseFolder, Path.Combine("bin", outputName));

            var args = new List<string>
            {
                "build",
                Path.Combine(sourceFolder, @"Package.wxs"),
                "-d", "V=" + defineV,
                "-d", "A=" + defineA,
                "-d", "B=" + defineB,
                "-bindpath", Path.Combine(sourceFolder, ".data"),
                "-intermediateFolder", Path.Combine(baseFolder, "obj"),
                "-o", outputPath,
                "-ext", extensionPath,
            };

            foreach (var additionaBindPath in bindpaths ?? Enumerable.Empty<string>())
            {
                args.Add("-bindpath");
                args.Add(additionaBindPath);
            }

            var result = WixRunner.Execute(args.ToArray());

            result.AssertSuccess();

            return Path.ChangeExtension(outputPath, ".wixpdb");
        }

        private static string BuildMsp(string outputName, string sourceFolder, string baseFolder, string defineV, IEnumerable<string> bindpaths = null, bool hasNoFiles = false, bool warningsAsErrors = true)
        {
            var outputPath = Path.Combine(baseFolder, Path.Combine("bin", outputName));

            var args = new List<string>
            {
                "build",
                hasNoFiles ? "-sw1079" : " ",
                Path.Combine(sourceFolder, @"Patch.wxs"),
                "-d", "V=" + defineV,
                "-bindpath", Path.Combine(baseFolder, "bin"),
                "-intermediateFolder", Path.Combine(baseFolder, "obj"),
                "-o", outputPath
            };

            foreach (var additionaBindPath in bindpaths ?? Enumerable.Empty<string>())
            {
                args.Add("-bindpath");
                args.Add(additionaBindPath);
            }

            var result = WixRunner.Execute(warningsAsErrors, args.ToArray());

            result.AssertSuccess();

            return Path.ChangeExtension(outputPath, ".wixpdb");
        }

        private static string BuildBundle(string outputName, string sourceFolder, string baseFolder)
        {
            var outputPath = Path.Combine(baseFolder, Path.Combine("bin", outputName));

            var result = WixRunner.Execute(new[]
            {
                "build",
                Path.Combine(sourceFolder, @"Bundle.wxs"),
                Path.Combine(sourceFolder, "..", "..", "BundleWithPackageGroupRef", "Bundle.wxs"),
                "-bindpath", Path.Combine(sourceFolder, "..", "..", "SimpleBundle", "data"),
                "-bindpath", Path.Combine(baseFolder, "bin"),
                "-intermediateFolder", Path.Combine(baseFolder, "obj"),
                "-o", outputPath
            });

            result.AssertSuccess();

            return Path.ChangeExtension(outputPath, ".wixpdb");
        }

        private static XDocument GetExtractPatchXml(string path)
        {
            var buffer = new StringBuilder(65535);
            var size = buffer.Capacity;

            var er = MsiExtractPatchXMLData(path, 0, buffer, ref size);
            if (er != 0)
            {
                throw new Win32Exception(er);
            }

            return XDocument.Parse(buffer.ToString());
        }

        private static void VerifyPatchTargetCodesInBurnManifest(string pdbPath, string[] expected)
        {
            using (var wixOutput = WixOutput.Read(pdbPath))
            {
                var manifestData = wixOutput.GetData(BurnConstants.BurnManifestWixOutputStreamName);
                var doc = new XmlDocument();
                doc.LoadXml(manifestData);
                var nsmgr = BundleExtractor.GetBurnNamespaceManager(doc, "w");
                var patchTargetCodes = doc.SelectNodes("/w:BurnManifest/w:PatchTargetCode", nsmgr).GetTestXmlLines();

                WixAssert.CompareLineByLine(expected, patchTargetCodes);
            }
        }

        [DllImport("msi.dll", EntryPoint = "MsiExtractPatchXMLDataW", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int MsiExtractPatchXMLData(string szPatchPath, int dwReserved, StringBuilder szXMLData, ref int pcchXMLData);

        [DllImport("msi.dll", EntryPoint = "MsiApplyPatchW", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int MsiApplyPatch(string szPatchPackage, string szInstallPackage, int eInstallType, string szCommandLine);
    }
}
