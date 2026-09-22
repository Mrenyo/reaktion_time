namespace ReactionTimeMeasurementApp
{
    partial class StatisticsView
    {
        private System.ComponentModel.IContainer components = null;
        private TableLayoutPanel rootLayout;
        private TableLayoutPanel contentLayout;
        private Label titleLabel;
        private Panel sessionSelectorPanel;
        private Label sessionSelectorLabel;
        private ComboBox sessionSelectorComboBox;
        private Label measurementTypeLabel;
        private Panel separatorLine;
        private DataGridView resultsGrid;
        private DataGridViewTextBoxColumn trialColumn;
        private DataGridViewTextBoxColumn espTimeColumn;
        private DataGridViewTextBoxColumn localTimeColumn;
        private DataGridViewTextBoxColumn statusColumn;
        private Panel averagePanel;
        private Label averageCaptionLabel;
        private Label averageValueLabel;
        private Label totalTrialsLabel;
        private Button backButton;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }

            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            rootLayout = new TableLayoutPanel();
            contentLayout = new TableLayoutPanel();
            titleLabel = new Label();
            sessionSelectorPanel = new Panel();
            sessionSelectorLabel = new Label();
            sessionSelectorComboBox = new ComboBox();
            measurementTypeLabel = new Label();
            separatorLine = new Panel();
            resultsGrid = new DataGridView();
            trialColumn = new DataGridViewTextBoxColumn();
            espTimeColumn = new DataGridViewTextBoxColumn();
            localTimeColumn = new DataGridViewTextBoxColumn();
            statusColumn = new DataGridViewTextBoxColumn();
            averagePanel = new Panel();
            averageCaptionLabel = new Label();
            averageValueLabel = new Label();
            totalTrialsLabel = new Label();
            backButton = new Button();
            rootLayout.SuspendLayout();
            contentLayout.SuspendLayout();
            sessionSelectorPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)resultsGrid).BeginInit();
            averagePanel.SuspendLayout();
            SuspendLayout();
            // 
            // rootLayout
            // 
            rootLayout.BackColor = Color.White;
            rootLayout.ColumnCount = 3;
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 17F));
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66F));
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 17F));
            rootLayout.Controls.Add(contentLayout, 1, 1);
            rootLayout.Dock = DockStyle.Fill;
            rootLayout.RowCount = 3;
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 4F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 92F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 4F));
            rootLayout.Size = new Size(1475, 820);
            rootLayout.TabIndex = 0;
            // 
            // contentLayout
            // 
            contentLayout.BackColor = Color.White;
            contentLayout.ColumnCount = 1;
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            contentLayout.Controls.Add(titleLabel, 0, 0);
            contentLayout.Controls.Add(sessionSelectorPanel, 0, 1);
            contentLayout.Controls.Add(measurementTypeLabel, 0, 2);
            contentLayout.Controls.Add(separatorLine, 0, 3);
            contentLayout.Controls.Add(resultsGrid, 0, 4);
            contentLayout.Controls.Add(averagePanel, 0, 5);
            contentLayout.Controls.Add(backButton, 0, 6);
            contentLayout.Dock = DockStyle.Fill;
            contentLayout.Location = new Point(250, 32);
            contentLayout.Margin = new Padding(0);
            contentLayout.RowCount = 7;
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 8F));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 6F));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 2F));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 58F));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 8F));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 8F));
            contentLayout.Size = new Size(973, 754);
            contentLayout.TabIndex = 0;
            // 
            // titleLabel
            // 
            titleLabel.Anchor = AnchorStyles.None;
            titleLabel.AutoSize = true;
            titleLabel.Font = new Font("Segoe UI Semibold", 34F, FontStyle.Bold, GraphicsUnit.Point, 238);
            titleLabel.ForeColor = Color.FromArgb(255, 91, 24);
            titleLabel.Location = new Point(326, 9);
            titleLabel.Name = "titleLabel";
            titleLabel.Size = new Size(320, 61);
            titleLabel.TabIndex = 0;
            titleLabel.Text = "Statisztika";
            //
            // sessionSelectorPanel
            //
            sessionSelectorPanel.BackColor = Color.White;
            sessionSelectorPanel.Controls.Add(sessionSelectorComboBox);
            sessionSelectorPanel.Controls.Add(sessionSelectorLabel);
            sessionSelectorPanel.Dock = DockStyle.Fill;
            sessionSelectorPanel.Location = new Point(0, 75);
            sessionSelectorPanel.Margin = new Padding(56, 4, 56, 4);
            sessionSelectorPanel.Name = "sessionSelectorPanel";
            sessionSelectorPanel.Size = new Size(861, 60);
            sessionSelectorPanel.TabIndex = 6;
            //
            // sessionSelectorLabel
            //
            sessionSelectorLabel.AutoSize = true;
            sessionSelectorLabel.Font = new Font("Segoe UI", 14F, FontStyle.Regular, GraphicsUnit.Point, 238);
            sessionSelectorLabel.ForeColor = Color.FromArgb(106, 106, 114);
            sessionSelectorLabel.Location = new Point(18, 17);
            sessionSelectorLabel.Name = "sessionSelectorLabel";
            sessionSelectorLabel.Size = new Size(130, 25);
            sessionSelectorLabel.TabIndex = 0;
            sessionSelectorLabel.Text = "Korábbi mérés:";
            //
            // sessionSelectorComboBox
            //
            sessionSelectorComboBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            sessionSelectorComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            sessionSelectorComboBox.Font = new Font("Segoe UI", 14F, FontStyle.Regular, GraphicsUnit.Point, 238);
            sessionSelectorComboBox.FormattingEnabled = true;
            sessionSelectorComboBox.Location = new Point(240, 13);
            sessionSelectorComboBox.Name = "sessionSelectorComboBox";
            sessionSelectorComboBox.Size = new Size(600, 33);
            sessionSelectorComboBox.TabIndex = 1;
            //
            // measurementTypeLabel
            //
            measurementTypeLabel.Dock = DockStyle.Fill;
            measurementTypeLabel.Font = new Font("Segoe UI", 17F, FontStyle.Regular, GraphicsUnit.Point, 238);
            measurementTypeLabel.ForeColor = Color.FromArgb(106, 106, 114);
            measurementTypeLabel.Location = new Point(0, 80);
            measurementTypeLabel.Name = "measurementTypeLabel";
            measurementTypeLabel.Size = new Size(973, 45);
            measurementTypeLabel.TabIndex = 1;
            measurementTypeLabel.Text = "Mérés típusa: még nincs adat";
            measurementTypeLabel.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // separatorLine
            // 
            separatorLine.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            separatorLine.BackColor = Color.FromArgb(236, 225, 214);
            separatorLine.Location = new Point(130, 128);
            separatorLine.Margin = new Padding(130, 2, 130, 0);
            separatorLine.Name = "separatorLine";
            separatorLine.Size = new Size(713, 2);
            separatorLine.TabIndex = 2;
            // 
            // resultsGrid
            // 
            resultsGrid.AllowUserToAddRows = false;
            resultsGrid.AllowUserToDeleteRows = false;
            resultsGrid.AllowUserToResizeColumns = false;
            resultsGrid.AllowUserToResizeRows = false;
            resultsGrid.BackgroundColor = Color.White;
            resultsGrid.BorderStyle = BorderStyle.None;
            resultsGrid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            resultsGrid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            resultsGrid.ColumnHeadersHeight = 52;
            resultsGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            resultsGrid.Columns.AddRange(new DataGridViewColumn[] { trialColumn, espTimeColumn, localTimeColumn, statusColumn });
            resultsGrid.Dock = DockStyle.Fill;
            resultsGrid.EnableHeadersVisualStyles = false;
            resultsGrid.GridColor = Color.FromArgb(238, 236, 234);
            resultsGrid.Location = new Point(56, 144);
            resultsGrid.Margin = new Padding(56, 8, 56, 6);
            resultsGrid.MultiSelect = false;
            resultsGrid.Name = "resultsGrid";
            resultsGrid.ReadOnly = true;
            resultsGrid.RowHeadersVisible = false;
            resultsGrid.RowTemplate.DefaultCellStyle.Font = new Font("Segoe UI", 16F, FontStyle.Regular, GraphicsUnit.Point, 238);
            resultsGrid.RowTemplate.Height = 42;
            resultsGrid.ScrollBars = ScrollBars.Vertical;
            resultsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            resultsGrid.Size = new Size(861, 466);
            resultsGrid.TabIndex = 3;
            // 
            // trialColumn
            // 
            trialColumn.FillWeight = 18F;
            trialColumn.HeaderText = "Mérés";
            trialColumn.Name = "trialColumn";
            trialColumn.ReadOnly = true;
            trialColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
            // 
            // espTimeColumn
            // 
            espTimeColumn.FillWeight = 26F;
            espTimeColumn.HeaderText = "ESP idő (ms)";
            espTimeColumn.Name = "espTimeColumn";
            espTimeColumn.ReadOnly = true;
            espTimeColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
            // 
            // localTimeColumn
            // 
            localTimeColumn.FillWeight = 26F;
            localTimeColumn.HeaderText = "Helyi idő (ms)";
            localTimeColumn.Name = "localTimeColumn";
            localTimeColumn.ReadOnly = true;
            localTimeColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
            // 
            // statusColumn
            // 
            statusColumn.FillWeight = 30F;
            statusColumn.HeaderText = "Státusz";
            statusColumn.Name = "statusColumn";
            statusColumn.ReadOnly = true;
            statusColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
            // 
            // averagePanel
            // 
            averagePanel.BackColor = Color.FromArgb(250, 243, 230);
            averagePanel.Controls.Add(totalTrialsLabel);
            averagePanel.Controls.Add(averageValueLabel);
            averagePanel.Controls.Add(averageCaptionLabel);
            averagePanel.Dock = DockStyle.Fill;
            averagePanel.Location = new Point(56, 634);
            averagePanel.Margin = new Padding(56, 4, 56, 4);
            averagePanel.Name = "averagePanel";
            averagePanel.Size = new Size(861, 52);
            averagePanel.TabIndex = 4;
            // 
            // averageCaptionLabel
            // 
            averageCaptionLabel.AutoSize = true;
            averageCaptionLabel.Font = new Font("Segoe UI Semibold", 17F, FontStyle.Bold, GraphicsUnit.Point, 238);
            averageCaptionLabel.ForeColor = Color.FromArgb(255, 91, 24);
            averageCaptionLabel.Location = new Point(18, 11);
            averageCaptionLabel.Name = "averageCaptionLabel";
            averageCaptionLabel.Size = new Size(144, 31);
            averageCaptionLabel.TabIndex = 0;
            averageCaptionLabel.Text = "Összesítés";
            //
            // averageValueLabel
            //
            averageValueLabel.AutoSize = true;
            averageValueLabel.Font = new Font("Segoe UI Semibold", 11.5F, FontStyle.Bold, GraphicsUnit.Point, 238);
            averageValueLabel.ForeColor = Color.FromArgb(255, 91, 24);
            averageValueLabel.Location = new Point(192, 5);
            averageValueLabel.Name = "averageValueLabel";
            averageValueLabel.Size = new Size(15, 21);
            averageValueLabel.TabIndex = 1;
            averageValueLabel.Text = "-";
            // 
            // totalTrialsLabel
            // 
            totalTrialsLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            totalTrialsLabel.AutoSize = true;
            totalTrialsLabel.Font = new Font("Segoe UI", 13F, FontStyle.Regular, GraphicsUnit.Point, 238);
            totalTrialsLabel.ForeColor = Color.FromArgb(118, 118, 126);
            totalTrialsLabel.Location = new Point(705, 15);
            totalTrialsLabel.Name = "totalTrialsLabel";
            totalTrialsLabel.Size = new Size(140, 25);
            totalTrialsLabel.TabIndex = 2;
            totalTrialsLabel.Text = "Érvényes: 0 / 0";
            // 
            // backButton
            // 
            backButton.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom;
            backButton.BackColor = Color.FromArgb(250, 236, 186);
            backButton.Cursor = Cursors.Hand;
            backButton.FlatAppearance.BorderSize = 0;
            backButton.FlatStyle = FlatStyle.Flat;
            backButton.Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold, GraphicsUnit.Point, 238);
            backButton.ForeColor = Color.FromArgb(164, 101, 20);
            backButton.Location = new Point(56, 697);
            backButton.Margin = new Padding(56, 2, 56, 2);
            backButton.Name = "backButton";
            backButton.Size = new Size(220, 50);
            backButton.TabIndex = 5;
            backButton.Text = "‹ Vissza";
            backButton.UseVisualStyleBackColor = false;
            backButton.Click += backButton_Click;
            // 
            // StatisticsView
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            Controls.Add(rootLayout);
            Name = "StatisticsView";
            Size = new Size(1475, 820);
            rootLayout.ResumeLayout(false);
            contentLayout.ResumeLayout(false);
            contentLayout.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)resultsGrid).EndInit();
            sessionSelectorPanel.ResumeLayout(false);
            sessionSelectorPanel.PerformLayout();
            averagePanel.ResumeLayout(false);
            averagePanel.PerformLayout();
            ResumeLayout(false);
        }
    }
}


