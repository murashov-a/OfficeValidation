﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using BrightIdeasSoftware;
using OfficeValidationLib.Classes;
using OfficeValidationLib.Classes.Session;
using OfficeValidationLib.Interfaces;
using ThreadState = System.Threading.ThreadState;

namespace OfficeValidationApp.UI
{
    public partial class MainForm : Form
    {
        private readonly DocumentManager _documentManager = new DocumentManager();
        private readonly SessionManager _sessionManager = new SessionManager("config.json");
        private readonly List<ISessionResults>  _sessionResults = new List<ISessionResults>();
        public MainForm()
        {
            InitializeComponent();
            SetupAspects();
        }

        void SetupAspects()
        {
            //prepare documents
            objectListViewDocuments.Objects = new List<IDocument>();
            olvColumnDocument.AspectGetter = rowObject => rowObject is IDocument document 
                ? document.Name 
                : null;
            olvColumnType.AspectGetter = rowObject => rowObject is IDocument document 
                ? document.Creator.Name 
                : null;

            //get document icon
            imageList.Images.Add(SystemIcons.WinLogo);
            //document type name, number in imageList
            var iconNumbersCache = new Dictionary<string, int>();
            olvColumnType.ImageGetter = delegate(object rowObject)
            {
                if (rowObject is IDocument document)
                {
                    if (!iconNumbersCache.ContainsKey(document.Creator.Name))
                    {
                        imageList.Images.Add(Icon.ExtractAssociatedIcon(document.Path));
                        iconNumbersCache.Add(document.Creator.Name, imageList.Images.Count - 1);
                    }
                    return iconNumbersCache[document.Creator.Name];
                }
                return 0;
            };
            objectListViewDocuments.RefreshObjects(objectListViewDocuments.Objects.Cast<IDocument>().ToList());
            objectListViewDocuments.ItemsChanged += (o, args) => UpdatePerformState();

            //checks
            objectListViewChecks.Objects = _sessionManager.Config.Instances.ToList();
            olvColumnCheck.AspectGetter = rowObject => rowObject is Instance instance 
                ? instance.DisplayName 
                : null;
            objectListViewChecks.RefreshObjects(objectListViewChecks.Objects.Cast<Instance>().ToList());
            objectListViewChecks.ItemChecked += (sender, args) => UpdatePerformState();
            objectListViewChecks.ItemsChanged += (sender, args) => UpdatePerformState();

            //tags
            objectListViewTags.Objects = _sessionManager.Config.Instances.SelectMany(x => x.Tags).Distinct().ToArray();
            olvColumnTag.AspectGetter = rowObject => rowObject;
            objectListViewTags.RefreshObjects(objectListViewTags.Objects.Cast<string>().ToList());

            //tags selection ('AND' behavior)
            objectListViewChecks.ModelFilter = new ModelFilter(objInstance => 
                objectListViewTags.CheckedObjects.Count == 0 ||
                objInstance is Instance instance && objectListViewTags.CheckedObjects.Count ==
                objectListViewTags.CheckedObjects.Cast<string>()
                .Intersect(instance.Tags).Count());
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e) => Close();

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e) => _sessionManager.Dispose();

        private void OpenDocumentsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            openFileDialogDocuments.Filter = $@"Все документы|{string.Join(";", _documentManager.DocumentFactories.SelectMany(x=>x.SupportingExtention.Select(y=>$"*{y}")).Distinct())}|" + 
                string.Join("|",
                    _documentManager.DocumentFactories
                .Select(x => $"{x.Name}|{string.Join(";", x.SupportingExtention.Select(y => $"*{y}"))}"));
            if (openFileDialogDocuments.ShowDialog() == DialogResult.OK)
            {
                AddDocuments(openFileDialogDocuments.FileNames);
            }
        }

        void AddDocuments(IEnumerable<string> documentPaths)
        {
            AddDocuments(documentPaths.SelectMany(file =>
                _documentManager.DocumentFactories
                    .Where(docFactory => docFactory.CanCreate(file))
                    .Select(docFactory => docFactory.Create(file))));
        }

        void AddDocuments(IEnumerable<IDocument> documents)
        {
            foreach (var document in documents)
            {
                if (!objectListViewDocuments.Objects.Cast<IDocument>().Contains(document))
                {
                    objectListViewDocuments.Objects = objectListViewDocuments.Objects
                        .Cast<IDocument>().Concat(new[] { document });

                }
            }
            objectListViewDocuments.RefreshObjects(objectListViewDocuments.Objects.Cast<IDocument>().ToList());
        }

        private void ObjectListViewChecks_SelectedIndexChanged(object sender, EventArgs e)
        {
            textBoxDescription.Text = objectListViewChecks.SelectedObject is Instance selectedInstance
                ? selectedInstance.Description
                : string.Empty;
        }

        private void ObjectListViewTags_ItemChecked(object sender, ItemCheckedEventArgs e)
        {

            objectListViewChecks.UpdateObjects(objectListViewChecks.Objects.Cast<object>().ToArray());
            UpdatePerformState();
        }

        void UpdatePerformState()
        {
            buttonPerform.Enabled = objectListViewDocuments.Objects.Cast<IDocument>().Any() && 
                                    objectListViewChecks.CheckedObjects.Count > 0;
        }


        private void ButtonPerform_Click(object sender, EventArgs e)
        {
            var splashForm = new SplashForm()
            {
                Message = "Пожалуйста, подождите"
            };
            var splashThread = new Thread(() => splashForm.ShowDialog());
            splashThread.Start();
            this.Hide();
            var session = _sessionManager.Create(
                objectListViewChecks.CheckedObjects.Cast<Instance>(),
                objectListViewDocuments.Objects.Cast<IDocument>());

            _sessionResults.Add(new SessionResults(session.PerformAll(), session));
            var resultForm = new ResultForm(_sessionResults);
            if (splashThread.ThreadState == ThreadState.Running)
            {
                splashForm.Invoke(new Action(() => splashForm.Close()));
            }
            resultForm.ShowDialog();
            this.Show();
        }

        private void ObjectListViewDocuments_DragEnter(object sender, DragEventArgs e)
        {
            //if contains document file
            if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
                ((string[])e.Data.GetData(DataFormats.FileDrop))
                .Any(x=>_documentManager.DocumentFactories.Any(y=>y.CanCreate(x))))
            {
                e.Effect = DragDropEffects.All;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void ObjectListViewDocuments_DragDrop(object sender, DragEventArgs e) =>
            AddDocuments((string[])e.Data.GetData(DataFormats.FileDrop));

        private void SupportingDocumentsToolStripMenuItem_Click(object sender, EventArgs e) =>
            new SupportingDocumentsForm(_documentManager.DocumentFactories).ShowDialog();

        private void RemoveOfListToolStripMenuItem_Click(object sender, EventArgs e) =>
            objectListViewDocuments.RemoveObjects(objectListViewDocuments.SelectedObjects);

        private void ShowInDirectoryToolStripMenuItem_Click(object sender, EventArgs e) =>
            objectListViewDocuments.SelectedObjects
                .Cast<IDocument>()
                .Select(document => Process.Start("explorer", $"/select,{document.Path}"))
                .ToArray();

        private void OpenToolStripMenuItem_Click(object sender, EventArgs e) =>
            objectListViewDocuments.SelectedObjects
                .Cast<IDocument>()
                .Select(document => Process.Start("explorer", document.Path))
                .ToArray();
    }
}
