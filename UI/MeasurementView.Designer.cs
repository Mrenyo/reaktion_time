namespace ReactionTimeMeasurementApp.UI
{
    partial class MeasurementView
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        private BufferedPanel stagePanel;
        private Panel lobbyCard;
        private Label titleLabel;
        private Label instructionLabel;
        private Label statusLabel;
        private Label summaryLabel;
        private Button startMeasureButton;
        private Button backButton;

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
            stagePanel = new BufferedPanel();
            lobbyCard = new Panel();
            titleLabel = new Label();
            instructionLabel = new Label();
            statusLabel = new Label();
            summaryLabel = new Label();
            startMeasureButton = new Button();
            backButton = new Button();
            stagePanel.SuspendLayout();
            lobbyCard.SuspendLayout();
            SuspendLayout();
            //
            // stagePanel
            //
            stagePanel.BackColor = Color.FromArgb(248, 244, 243);
            stagePanel.Controls.Add(lobbyCard);
            stagePanel.Dock = DockStyle.Fill;
            stagePanel.Location = new Point(0, 0);
            stagePanel.Margin = new Padding(0);
            stagePanel.Name = "stagePanel";
            stagePanel.Size = new Size(1475, 628);
            stagePanel.TabIndex = 0;
            stagePanel.Paint += stagePanel_Paint;
            stagePanel.MouseDown += stagePanel_MouseDown;
            stagePanel.Resize += stagePanel_Resize;
            //
            // lobbyCard
            //
            lobbyCard.BackColor = Color.FromArgb(253, 250, 250);
            lobbyCard.Controls.Add(titleLabel);
            lobbyCard.Controls.Add(instructionLabel);
            lobbyCard.Controls.Add(statusLabel);
            lobbyCard.Controls.Add(summaryLabel);
            lobbyCard.Controls.Add(startMeasureButton);
            lobbyCard.Controls.Add(backButton);
            lobbyCard.Location = new Point(357, 109);
            lobbyCard.Margin = new Padding(0);
            lobbyCard.Name = "lobbyCard";
            lobbyCard.Size = new Size(760, 410);
            lobbyCard.TabIndex = 0;
            //
            // titleLabel
            //
            titleLabel.AutoSize = true;
            titleLabel.BackColor = Color.FromArgb(253, 250, 250);
            titleLabel.Font = new Font("Segoe UI Semibold", 22F, FontStyle.Bold, GraphicsUnit.Point, 238);
            titleLabel.ForeColor = Color.FromArgb(255, 91, 24);
            titleLabel.Location = new Point(242, 36);
            titleLabel.Margin = new Padding(0);
            titleLabel.MaximumSize = new Size(680, 0);
            titleLabel.Name = "titleLabel";
            titleLabel.Size = new Size(276, 41);
            titleLabel.TabIndex = 0;
            titleLabel.Text = "Reakcióidő-mérés";
            titleLabel.TextAlign = ContentAlignment.TopCenter;
            //
            // instructionLabel
            //
            instructionLabel.AutoSize = true;
            instructionLabel.BackColor = Color.FromArgb(253, 250, 250);
            instructionLabel.Font = new Font("Segoe UI", 14F, FontStyle.Regular, GraphicsUnit.Point, 238);
            instructionLabel.ForeColor = Color.FromArgb(64, 70, 82);
            instructionLabel.Location = new Point(40, 91);
            instructionLabel.Margin = new Padding(0);
            instructionLabel.MaximumSize = new Size(680, 0);
            instructionLabel.Name = "instructionLabel";
            instructionLabel.Size = new Size(680, 75);
            instructionLabel.TabIndex = 1;
            instructionLabel.TextAlign = ContentAlignment.TopCenter;
            //
            // statusLabel
            //
            statusLabel.AutoSize = true;
            statusLabel.BackColor = Color.FromArgb(253, 250, 250);
            statusLabel.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point, 238);
            statusLabel.ForeColor = Color.FromArgb(52, 58, 70);
            statusLabel.Location = new Point(40, 184);
            statusLabel.Margin = new Padding(0);
            statusLabel.MaximumSize = new Size(680, 0);
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new Size(680, 21);
            statusLabel.TabIndex = 2;
            statusLabel.TextAlign = ContentAlignment.TopCenter;
            statusLabel.Visible = false;
            //
            // summaryLabel
            //
            summaryLabel.AutoSize = true;
            summaryLabel.BackColor = Color.FromArgb(253, 250, 250);
            summaryLabel.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular, GraphicsUnit.Point, 238);
            summaryLabel.ForeColor = Color.FromArgb(96, 106, 120);
            summaryLabel.Location = new Point(40, 219);
            summaryLabel.Margin = new Padding(0);
            summaryLabel.MaximumSize = new Size(680, 0);
            summaryLabel.Name = "summaryLabel";
            summaryLabel.Size = new Size(680, 38);
            summaryLabel.TabIndex = 3;
            summaryLabel.TextAlign = ContentAlignment.TopCenter;
            //
            // startMeasureButton
            //
            startMeasureButton.BackColor = Color.FromArgb(255, 91, 24);
            startMeasureButton.Cursor = Cursors.Hand;
            startMeasureButton.FlatAppearance.BorderColor = Color.FromArgb(255, 91, 24);
            startMeasureButton.FlatAppearance.BorderSize = 0;
            startMeasureButton.FlatStyle = FlatStyle.Flat;
            startMeasureButton.Font = new Font("Segoe UI Semibold", 14F, FontStyle.Bold, GraphicsUnit.Point, 238);
            startMeasureButton.ForeColor = Color.White;
            startMeasureButton.Location = new Point(139, 283);
            startMeasureButton.Name = "startMeasureButton";
            startMeasureButton.Size = new Size(260, 54);
            startMeasureButton.TabIndex = 4;
            startMeasureButton.Text = "Mérés indítása";
            startMeasureButton.UseVisualStyleBackColor = false;
            startMeasureButton.Click += startMeasureButton_Click;
            //
            // backButton
            //
            backButton.BackColor = Color.FromArgb(244, 238, 237);
            backButton.Cursor = Cursors.Hand;
            backButton.FlatAppearance.BorderColor = Color.FromArgb(244, 238, 237);
            backButton.FlatAppearance.BorderSize = 0;
            backButton.FlatStyle = FlatStyle.Flat;
            backButton.Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold, GraphicsUnit.Point, 238);
            backButton.ForeColor = Color.FromArgb(85, 94, 114);
            backButton.Location = new Point(415, 283);
            backButton.Name = "backButton";
            backButton.Size = new Size(206, 54);
            backButton.TabIndex = 5;
            backButton.Text = "Vissza a menühöz";
            backButton.UseVisualStyleBackColor = false;
            backButton.Click += backButton_Click;
            //
            // MeasurementView
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(248, 244, 243);
            Controls.Add(stagePanel);
            Name = "MeasurementView";
            Size = new Size(1475, 628);
            stagePanel.ResumeLayout(false);
            lobbyCard.ResumeLayout(false);
            lobbyCard.PerformLayout();
            ResumeLayout(false);
        }

        #endregion
    }
}
