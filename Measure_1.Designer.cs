namespace ReactionTimeMeasurementApp
{
    partial class Measure_1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        private TableLayoutPanel rootLayout;
        private Panel stagePanel;
        private Label instructionLabel;
        private TableLayoutPanel bottomBarLayout;
        private Button backButton;
        private Button startMeasureButton;
        private FlowLayoutPanel statusFlowPanel;
        private Label statusTitleLabel;
        private Label statusValueLabel;
        private Label lastResultTitleLabel;
        private Label lastResultValueLabel;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        private void InitializeComponent()
        {
            rootLayout = new TableLayoutPanel();
            stagePanel = new Panel();
            instructionLabel = new Label();
            bottomBarLayout = new TableLayoutPanel();
            backButton = new Button();
            startMeasureButton = new Button();
            statusFlowPanel = new FlowLayoutPanel();
            statusTitleLabel = new Label();
            statusValueLabel = new Label();
            lastResultTitleLabel = new Label();
            lastResultValueLabel = new Label();
            rootLayout.SuspendLayout();
            stagePanel.SuspendLayout();
            bottomBarLayout.SuspendLayout();
            statusFlowPanel.SuspendLayout();
            SuspendLayout();
            // 
            // rootLayout
            // 
            rootLayout.ColumnCount = 1;
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rootLayout.Controls.Add(stagePanel, 0, 0);
            rootLayout.Controls.Add(bottomBarLayout, 0, 1);
            rootLayout.Dock = DockStyle.Fill;
            rootLayout.Location = new Point(0, 0);
            rootLayout.Margin = new Padding(0);
            rootLayout.Name = "rootLayout";
            rootLayout.RowCount = 2;
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110F));
            rootLayout.Size = new Size(1475, 628);
            rootLayout.TabIndex = 0;
            // 
            // stagePanel
            // 
            stagePanel.BackColor = Color.FromArgb(30, 30, 30);
            stagePanel.Controls.Add(instructionLabel);
            stagePanel.Dock = DockStyle.Fill;
            stagePanel.Location = new Point(0, 0);
            stagePanel.Margin = new Padding(0);
            stagePanel.Name = "stagePanel";
            stagePanel.Size = new Size(1475, 518);
            stagePanel.TabIndex = 0;
            stagePanel.Paint += stagePanel_Paint;
            // 
            // instructionLabel
            // 
            instructionLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            instructionLabel.AutoSize = false;
            instructionLabel.BackColor = Color.Transparent;
            instructionLabel.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 238);
            instructionLabel.ForeColor = Color.FromArgb(205, 214, 224);
            instructionLabel.Location = new Point(40, 24);
            instructionLabel.Margin = new Padding(40, 24, 40, 0);
            instructionLabel.Name = "instructionLabel";
            instructionLabel.Size = new Size(1395, 84);
            instructionLabel.TabIndex = 0;
            instructionLabel.Text = "";
            instructionLabel.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // bottomBarLayout
            // 
            bottomBarLayout.BackColor = Color.FromArgb(22, 22, 22);
            bottomBarLayout.ColumnCount = 3;
            bottomBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33333F));
            bottomBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33333F));
            bottomBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33333F));
            bottomBarLayout.Controls.Add(backButton, 0, 0);
            bottomBarLayout.Controls.Add(startMeasureButton, 1, 0);
            bottomBarLayout.Controls.Add(statusFlowPanel, 2, 0);
            bottomBarLayout.Dock = DockStyle.Fill;
            bottomBarLayout.Location = new Point(0, 518);
            bottomBarLayout.Margin = new Padding(0);
            bottomBarLayout.Name = "bottomBarLayout";
            bottomBarLayout.Padding = new Padding(24, 18, 24, 18);
            bottomBarLayout.RowCount = 1;
            bottomBarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            bottomBarLayout.Size = new Size(1475, 110);
            bottomBarLayout.TabIndex = 1;
            // 
            // backButton
            // 
            backButton.BackColor = Color.FromArgb(241, 244, 247);
            backButton.Cursor = Cursors.Hand;
            backButton.Dock = DockStyle.Left;
            backButton.FlatAppearance.BorderSize = 0;
            backButton.FlatStyle = FlatStyle.Flat;
            backButton.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold, GraphicsUnit.Point, 238);
            backButton.ForeColor = Color.FromArgb(20, 71, 76);
            backButton.Location = new Point(27, 21);
            backButton.Name = "backButton";
            backButton.Size = new Size(176, 68);
            backButton.TabIndex = 0;
            backButton.Text = "Vissza a menühöz";
            backButton.UseVisualStyleBackColor = false;
            backButton.Click += backButton_Click;
            // 
            // startMeasureButton
            // 
            startMeasureButton.Anchor = AnchorStyles.None;
            startMeasureButton.BackColor = Color.FromArgb(255, 91, 24);
            startMeasureButton.Cursor = Cursors.Hand;
            startMeasureButton.FlatAppearance.BorderSize = 0;
            startMeasureButton.FlatStyle = FlatStyle.Flat;
            startMeasureButton.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold, GraphicsUnit.Point, 238);
            startMeasureButton.ForeColor = Color.White;
            startMeasureButton.Location = new Point(612, 28);
            startMeasureButton.Name = "startMeasureButton";
            startMeasureButton.Size = new Size(250, 54);
            startMeasureButton.TabIndex = 1;
            startMeasureButton.Text = "Mérés indítása";
            startMeasureButton.UseVisualStyleBackColor = false;
            startMeasureButton.Click += startMeasureButton_Click;
            // 
            // statusFlowPanel
            // 
            statusFlowPanel.Anchor = AnchorStyles.Right;
            statusFlowPanel.AutoSize = true;
            statusFlowPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            statusFlowPanel.Controls.Add(statusTitleLabel);
            statusFlowPanel.Controls.Add(statusValueLabel);
            statusFlowPanel.Controls.Add(lastResultTitleLabel);
            statusFlowPanel.Controls.Add(lastResultValueLabel);
            statusFlowPanel.Location = new Point(1225, 24);
            statusFlowPanel.Margin = new Padding(0);
            statusFlowPanel.Name = "statusFlowPanel";
            statusFlowPanel.SetFlowBreak(statusValueLabel, true);
            statusFlowPanel.Size = new Size(223, 61);
            statusFlowPanel.TabIndex = 2;
            //
            // statusTitleLabel
            //
            statusTitleLabel.AutoSize = true;
            statusTitleLabel.Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold, GraphicsUnit.Point, 238);
            statusTitleLabel.ForeColor = Color.WhiteSmoke;
            statusTitleLabel.Location = new Point(0, 0);
            statusTitleLabel.Margin = new Padding(0, 0, 6, 6);
            statusTitleLabel.Name = "statusTitleLabel";
            statusTitleLabel.Size = new Size(89, 19);
            statusTitleLabel.TabIndex = 0;
            statusTitleLabel.Text = "Folyamat állapota:";
            //
            // statusValueLabel
            //
            statusValueLabel.AutoSize = true;
            statusValueLabel.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular, GraphicsUnit.Point, 238);
            statusValueLabel.ForeColor = Color.FromArgb(205, 214, 224);
            statusValueLabel.Location = new Point(95, 0);
            statusValueLabel.Margin = new Padding(0, 0, 0, 6);
            statusValueLabel.Name = "statusValueLabel";
            statusValueLabel.Size = new Size(31, 19);
            statusValueLabel.TabIndex = 1;
            statusValueLabel.Text = "0/10";
            //
            // lastResultTitleLabel
            //
            lastResultTitleLabel.AutoSize = true;
            lastResultTitleLabel.Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold, GraphicsUnit.Point, 238);
            lastResultTitleLabel.ForeColor = Color.WhiteSmoke;
            lastResultTitleLabel.Location = new Point(0, 25);
            lastResultTitleLabel.Margin = new Padding(0, 0, 6, 0);
            lastResultTitleLabel.Name = "lastResultTitleLabel";
            lastResultTitleLabel.Size = new Size(120, 19);
            lastResultTitleLabel.TabIndex = 2;
            lastResultTitleLabel.Text = "Legutóbbi eredmény:";
            // 
            // lastResultValueLabel
            // 
            lastResultValueLabel.AutoSize = true;
            lastResultValueLabel.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular, GraphicsUnit.Point, 238);
            lastResultValueLabel.ForeColor = Color.FromArgb(205, 214, 224);
            lastResultValueLabel.Location = new Point(126, 25);
            lastResultValueLabel.Margin = new Padding(0);
            lastResultValueLabel.Name = "lastResultValueLabel";
            lastResultValueLabel.Size = new Size(14, 19);
            lastResultValueLabel.TabIndex = 3;
            lastResultValueLabel.Text = "-";
            // 
            // Measure_1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(30, 30, 30);
            Controls.Add(rootLayout);
            Name = "Measure_1";
            Size = new Size(1475, 628);
            rootLayout.ResumeLayout(false);
            stagePanel.ResumeLayout(false);
            stagePanel.PerformLayout();
            bottomBarLayout.ResumeLayout(false);
            bottomBarLayout.PerformLayout();
            statusFlowPanel.ResumeLayout(false);
            statusFlowPanel.PerformLayout();
            ResumeLayout(false);
        }

        #endregion
    }
}
