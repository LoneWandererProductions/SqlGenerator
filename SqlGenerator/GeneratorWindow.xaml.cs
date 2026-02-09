/*
 * COPYRIGHT:   See COPYING in the top level directory
 * PROJECT:     SqlGenerator
 * FILE:        GeneratorWindow.xaml.cs
 * PURPOSE:     Main window for the SQL table generator application, allowing users to input column definitions and generate CREATE TABLE scripts.
 * PROGRAMMER:  Peter Geinitz (Wayfarer)
 */

using Microsoft.Win32;
using SqlGenerator;
using System.IO;
using System.Text;
using System.Windows;

namespace TableGenerator
{
    /// <summary>
    /// Main window for the SQL table generator application, allowing users to input column definitions and generate CREATE TABLE scripts.
    /// </summary>
    public partial class GeneratorWindow : Window
    {
        /// <summary>
        /// The generator
        /// </summary>
        private readonly SqlQuery _generator = new SqlQuery();

        /// <summary>
        /// Initializes a new instance of the <see cref="GeneratorWindow"/> class.
        /// </summary>
        public GeneratorWindow()
        {
            InitializeComponent();

            // sample starter text
            TxtInput.Text = "Id INT NOT NULL \"Primary key\"\n" +
                            "Name NVARCHAR(250) NULL \"Display name\"\n" +
                            "Description NVARCHAR(MAX) NULL \"Optional details\"\n" +
                            "CreatedAt DATETIME NOT NULL \"Creation timestamp\"\n" +
                            "IsActive BIT NOT NULL \"Active flag\"";
            RefreshPreview();

            TxtInput.TextChanged += (s, e) => RefreshPreview();
        }

        /// <summary>
        /// Refreshes the preview.
        /// </summary>
        private void RefreshPreview()
        {
            var cols = SqlQuery.ParseColumns(TxtInput.Text);
            GridPreview.ItemsSource = cols;
        }

        /// <summary>
        /// Handles the Click event of the BtnGenerate control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            var tableName = TxtTableName.Text.Trim();
            var database = TxtDatabaseName.Text.Trim();
            if (string.IsNullOrEmpty(database))
            {
                database = "dbo.Replace";
            }

            if (string.IsNullOrEmpty(tableName))
            {
                MessageBox.Show("Please provide a table name.", "Missing table name", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var includeAudit = ChkIncludeAudit.IsChecked == true;
            var cols = SqlQuery.ParseColumns(TxtInput.Text);

            if (cols.Count == 0)
            {
                MessageBox.Show("Please provide at least one column definition.", "No columns defined", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var index = TxtIndexColumn.Text.Trim();

            TxtOutput.Text = _generator.GenerateCreateScript(database, tableName, cols, includeAudit, index);
        }

        /// <summary>
        /// Handles the Click event of the BtnCopy control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TxtOutput.Text))
                Clipboard.SetText(TxtOutput.Text);
        }

        /// <summary>
        /// Handles the Click event of the BtnSave control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "SQL files|*.sql|All files|*.*", FileName = TxtTableName.Text + ".sql" };
            if (dlg.ShowDialog(this) == true)
            {
                File.WriteAllText(dlg.FileName, TxtOutput.Text, Encoding.UTF8);
            }
        }
    }
}