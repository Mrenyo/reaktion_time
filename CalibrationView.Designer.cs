namespace ReactionTimeMeasurementApp
{
    partial class CalibrationView
    {
        private System.ComponentModel.IContainer components = null;
        private TableLayoutPanel rootLayout;
        private TableLayoutPanel contentLayout;
        private Label titleLabel;
        private Panel dividerPanel;
        private PictureBox calibrationPictureBox;
        private Label promptLabel;
        private TableLayoutPanel buttonLayout;
        private Button backButton;
        private Button recalibrationButton;
        private Button currentConfigurationButton;

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
            dividerPanel = new Panel();
            calibrationPictureBox = new PictureBox();
            promptLabel = new Label();
            buttonLayout = new TableLayoutPanel();
            backButton = new Button();
            recalibrationButton = new Button();
            currentConfigurationButton = new Button();
            rootLayout.SuspendLayout();
            contentLayout.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)calibrationPictureBox).BeginInit();
            buttonLayout.SuspendLayout();
            SuspendLayout();
            // 
            // rootLayout
            // 
            rootLayout.BackColor = Color.Transparent;
            rootLayout.ColumnCount = 3;
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22F));
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56F));
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22F));
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
            contentLayout.BackColor = Color.Transparent;
            contentLayout.ColumnCount = 1;
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            contentLayout.Controls.Add(titleLabel, 0, 0);
            contentLayout.Controls.Add(dividerPanel, 0, 1);
            contentLayout.Controls.Add(calibrationPictureBox, 0, 2);
            contentLayout.Controls.Add(promptLabel, 0, 3);
            contentLayout.Controls.Add(buttonLayout, 0, 4);
            contentLayout.Dock = DockStyle.Fill;
            contentLayout.Location = new Point(324, 32);
            contentLayout.Margin = new Padding(0);
            contentLayout.RowCount = 5;
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 12F));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 3F));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 48F));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 12F));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
            contentLayout.Size = new Size(826, 754);
            contentLayout.TabIndex = 0;
            // 
            // titleLabel
            // 
            titleLabel.Anchor = AnchorStyles.None;
            titleLabel.AutoSize = true;
            titleLabel.Font = new Font("Segoe UI Semibold", 32F, FontStyle.Bold, GraphicsUnit.Point, 238);
            titleLabel.ForeColor = Color.FromArgb(255, 91, 24);
            titleLabel.Location = new Point(278, 14);
            titleLabel.Name = "titleLabel";
            titleLabel.Size = new Size(270, 59);
            titleLabel.TabIndex = 0;
            titleLabel.Text = "Kalibráció";
            titleLabel.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // dividerPanel
            // 
            dividerPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            dividerPanel.BackColor = Color.FromArgb(239, 225, 219);
            dividerPanel.Location = new Point(130, 104);
            dividerPanel.Margin = new Padding(130, 12, 130, 0);
            dividerPanel.Name = "dividerPanel";
            dividerPanel.Size = new Size(566, 2);
            dividerPanel.TabIndex = 1;
            // 
            // calibrationPictureBox
            // 
            calibrationPictureBox.Dock = DockStyle.Fill;
            calibrationPictureBox.BackColor = Color.Transparent;
            calibrationPictureBox.Location = new Point(153, 126);
            calibrationPictureBox.Margin = new Padding(153, 18, 153, 0);
            calibrationPictureBox.Name = "calibrationPictureBox";
            calibrationPictureBox.Size = new Size(520, 344);
            calibrationPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            calibrationPictureBox.TabIndex = 2;
            calibrationPictureBox.TabStop = false;
            // 
            // promptLabel
            // 
            promptLabel.AutoSize = false;
            promptLabel.Dock = DockStyle.Fill;
            promptLabel.Font = new Font("Segoe UI", 20F, FontStyle.Regular, GraphicsUnit.Point, 238);
            promptLabel.ForeColor = Color.FromArgb(117, 118, 126);
            promptLabel.Location = new Point(153, 477);
            promptLabel.Margin = new Padding(153, 0, 153, 0);
            promptLabel.Name = "promptLabel";
            promptLabel.Size = new Size(520, 90);
            promptLabel.TabIndex = 3;
            promptLabel.Text = "Válassz az alábbi lehetőségek közül:";
            promptLabel.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // buttonLayout
            // 
            buttonLayout.BackColor = Color.Transparent;
            buttonLayout.ColumnCount = 1;
            buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            buttonLayout.Controls.Add(currentConfigurationButton, 0, 0);
            buttonLayout.Controls.Add(recalibrationButton, 0, 1);
            buttonLayout.Controls.Add(backButton, 0, 2);
            buttonLayout.Dock = DockStyle.Fill;
            buttonLayout.Location = new Point(153, 560);
            buttonLayout.Margin = new Padding(153, 0, 153, 8);
            buttonLayout.RowCount = 3;
            buttonLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3333321F));
            buttonLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3333321F));
            buttonLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3333321F));
            buttonLayout.Size = new Size(520, 234);
            buttonLayout.TabIndex = 4;
            // 
            // backButton
            // 
            backButton.BackColor = Color.FromArgb(250, 236, 186);
            backButton.Cursor = Cursors.Hand;
            backButton.Dock = DockStyle.Fill;
            backButton.FlatAppearance.BorderSize = 0;
            backButton.FlatStyle = FlatStyle.Flat;
            backButton.Font = new Font("Segoe UI Semibold", 19F, FontStyle.Bold, GraphicsUnit.Point, 238);
            backButton.ForeColor = Color.FromArgb(164, 101, 20);
            backButton.Margin = new Padding(8, 8, 8, 8);
            backButton.Name = "backButton";
            backButton.Padding = new Padding(16, 0, 0, 0);
            backButton.Size = new Size(504, 62);
            backButton.TabIndex = 2;
            backButton.Text = "‹ Vissza";
            backButton.TextAlign = ContentAlignment.MiddleLeft;
            backButton.UseVisualStyleBackColor = false;
            backButton.Click += backButton_Click;
            // 
            // recalibrationButton
            // 
            recalibrationButton.BackColor = Color.FromArgb(255, 91, 24);
            recalibrationButton.Cursor = Cursors.Hand;
            recalibrationButton.Dock = DockStyle.Fill;
            recalibrationButton.FlatAppearance.BorderSize = 0;
            recalibrationButton.FlatStyle = FlatStyle.Flat;
            recalibrationButton.Font = new Font("Segoe UI Semibold", 19F, FontStyle.Bold, GraphicsUnit.Point, 238);
            recalibrationButton.ForeColor = Color.White;
            recalibrationButton.Margin = new Padding(8, 8, 8, 8);
            recalibrationButton.Name = "recalibrationButton";
            recalibrationButton.Padding = new Padding(16, 0, 0, 0);
            recalibrationButton.Size = new Size(504, 62);
            recalibrationButton.TabIndex = 1;
            recalibrationButton.Text = "↻ Újrakalibrálás";
            recalibrationButton.TextAlign = ContentAlignment.MiddleLeft;
            recalibrationButton.UseVisualStyleBackColor = false;
            recalibrationButton.Click += recalibrationButton_Click;
            // 
            // currentConfigurationButton
            // 
            currentConfigurationButton.BackColor = Color.FromArgb(255, 91, 24);
            currentConfigurationButton.Cursor = Cursors.Hand;
            currentConfigurationButton.Dock = DockStyle.Fill;
            currentConfigurationButton.FlatAppearance.BorderSize = 0;
            currentConfigurationButton.FlatStyle = FlatStyle.Flat;
            currentConfigurationButton.Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold, GraphicsUnit.Point, 238);
            currentConfigurationButton.ForeColor = Color.White;
            currentConfigurationButton.Margin = new Padding(8, 8, 8, 8);
            currentConfigurationButton.Name = "currentConfigurationButton";
            currentConfigurationButton.Padding = new Padding(16, 0, 0, 0);
            currentConfigurationButton.Size = new Size(504, 62);
            currentConfigurationButton.TabIndex = 0;
            currentConfigurationButton.Text = "✓ Meglévő kalibráció használata";
            currentConfigurationButton.TextAlign = ContentAlignment.MiddleLeft;
            currentConfigurationButton.UseVisualStyleBackColor = false;
            currentConfigurationButton.Click += currentConfigurationButton_Click;
            // 
            // CalibrationView
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(248, 244, 243);
            Controls.Add(rootLayout);
            Name = "CalibrationView";
            Size = new Size(1475, 820);
            rootLayout.ResumeLayout(false);
            contentLayout.ResumeLayout(false);
            contentLayout.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)calibrationPictureBox).EndInit();
            buttonLayout.ResumeLayout(false);
            ResumeLayout(false);
        }
    }
}
