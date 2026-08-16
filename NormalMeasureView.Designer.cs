namespace ReactionTimeMeasurementApp
{
    partial class NormalMeasureView
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        private TableLayoutPanel rootLayout;
        private Panel stagePanel;
        private TableLayoutPanel bottomBarLayout;
        private Button backButton;
        private Button startMeasureButton;
        private Label instructionLabel;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources are being disposed; otherwise, false.</param>
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(NormalMeasureView));
            rootLayout = new TableLayoutPanel();
            stagePanel = new Panel();
            instructionLabel = new Label();
            bottomBarLayout = new TableLayoutPanel();
            backButton = new Button();
            startMeasureButton = new Button();
            rootLayout.SuspendLayout();
            stagePanel.SuspendLayout();
            bottomBarLayout.SuspendLayout();
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
            rootLayout.Size = new Size(1448, 656);
            rootLayout.TabIndex = 0;
            // 
            // stagePanel
            // 
            stagePanel.Controls.Add(instructionLabel);
            stagePanel.Dock = DockStyle.Fill;
            stagePanel.Location = new Point(0, 0);
            stagePanel.Margin = new Padding(0);
            stagePanel.Name = "stagePanel";
            stagePanel.Size = new Size(1448, 546);
            stagePanel.TabIndex = 0;
            stagePanel.Paint += NormalMeasureView_Paint;
            stagePanel.MouseClick += stagePanel_MouseClick;
            // 
            // instructionLabel
            // 
            instructionLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            instructionLabel.BackColor = Color.Transparent;
            instructionLabel.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 238);
            instructionLabel.ForeColor = Color.FromArgb(236, 240, 245);
            instructionLabel.Location = new Point(40, 24);
            instructionLabel.Margin = new Padding(40, 24, 40, 0);
            instructionLabel.Name = "instructionLabel";
            instructionLabel.Size = new Size(1368, 56);
            instructionLabel.TabIndex = 0;
            instructionLabel.Text = resources.GetString("instructionLabel.Text");
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
            bottomBarLayout.Dock = DockStyle.Fill;
            bottomBarLayout.Location = new Point(0, 546);
            bottomBarLayout.Margin = new Padding(0);
            bottomBarLayout.Name = "bottomBarLayout";
            bottomBarLayout.Padding = new Padding(24, 18, 24, 18);
            bottomBarLayout.RowCount = 1;
            bottomBarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            bottomBarLayout.Size = new Size(1448, 110);
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
            startMeasureButton.Location = new Point(598, 28);
            startMeasureButton.Name = "startMeasureButton";
            startMeasureButton.Size = new Size(250, 54);
            startMeasureButton.TabIndex = 1;
            startMeasureButton.Text = "Mérés indítása";
            startMeasureButton.UseVisualStyleBackColor = false;
            startMeasureButton.Click += startMeasureButton_Click;
            // 
            // NormalMeasureView
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(30, 30, 30);
            Controls.Add(rootLayout);
            Name = "NormalMeasureView";
            Size = new Size(1448, 656);
            rootLayout.ResumeLayout(false);
            stagePanel.ResumeLayout(false);
            bottomBarLayout.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion
    }
}
