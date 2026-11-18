namespace ReactionTimeMeasurementApp.UI;

partial class HardwareSettingsTab
{
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel hardwareLayout;
    private Label espPortLabel;
    private ComboBox espPortComboBox;
    private Label espPortHintLabel;
    private Label baudRateLabel;
    private ComboBox baudRateComboBox;
    private Label baudRateHintLabel;
    private Label photodiodeHeaderLabel;
    private FlowLayoutPanel photodiodeButtonsPanel;
    private Button movePhotodiodeButton;
    private Button resetPhotodiodeButton;
    private Label photodiodePositionLabel;
    private Label photodiodeHintLabel;

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
        hardwareLayout = new TableLayoutPanel();
        espPortLabel = new Label();
        espPortComboBox = new ComboBox();
        espPortHintLabel = new Label();
        baudRateLabel = new Label();
        baudRateComboBox = new ComboBox();
        baudRateHintLabel = new Label();
        photodiodeHeaderLabel = new Label();
        photodiodeButtonsPanel = new FlowLayoutPanel();
        movePhotodiodeButton = new Button();
        resetPhotodiodeButton = new Button();
        photodiodePositionLabel = new Label();
        photodiodeHintLabel = new Label();
        hardwareLayout.SuspendLayout();
        photodiodeButtonsPanel.SuspendLayout();
        SuspendLayout();
        // hardwareLayout
        //
        hardwareLayout.AutoSize = true;
        hardwareLayout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        hardwareLayout.ColumnCount = 2;
        hardwareLayout.ColumnStyles.Add(new ColumnStyle());
        hardwareLayout.ColumnStyles.Add(new ColumnStyle());
        hardwareLayout.Controls.Add(espPortLabel, 0, 0);
        hardwareLayout.Controls.Add(espPortComboBox, 1, 0);
        hardwareLayout.Controls.Add(espPortHintLabel, 0, 1);
        hardwareLayout.Controls.Add(baudRateLabel, 0, 2);
        hardwareLayout.Controls.Add(baudRateComboBox, 1, 2);
        hardwareLayout.Controls.Add(baudRateHintLabel, 0, 3);
        hardwareLayout.Controls.Add(photodiodeHeaderLabel, 0, 4);
        hardwareLayout.Controls.Add(photodiodeButtonsPanel, 0, 5);
        hardwareLayout.Controls.Add(photodiodePositionLabel, 0, 6);
        hardwareLayout.Controls.Add(photodiodeHintLabel, 0, 7);
        hardwareLayout.Location = new Point(0, 0);
        hardwareLayout.Margin = new Padding(0);
        hardwareLayout.Name = "hardwareLayout";
        hardwareLayout.RowCount = 8;
        hardwareLayout.RowStyles.Add(new RowStyle());
        hardwareLayout.RowStyles.Add(new RowStyle());
        hardwareLayout.RowStyles.Add(new RowStyle());
        hardwareLayout.RowStyles.Add(new RowStyle());
        hardwareLayout.RowStyles.Add(new RowStyle());
        hardwareLayout.RowStyles.Add(new RowStyle());
        hardwareLayout.RowStyles.Add(new RowStyle());
        hardwareLayout.RowStyles.Add(new RowStyle());
        hardwareLayout.Size = new Size(470, 300);
        hardwareLayout.TabIndex = 0;
        //
        // espPortLabel
        //
        espPortLabel.Anchor = AnchorStyles.Left;
        espPortLabel.AutoSize = true;
        espPortLabel.Margin = new Padding(0, 4, 10, 4);
        espPortLabel.Name = "espPortLabel";
        espPortLabel.TabIndex = 0;
        espPortLabel.Text = "ESP soros port";
        //
        // espPortComboBox
        //
        espPortComboBox.Anchor = AnchorStyles.Left;
        espPortComboBox.Margin = new Padding(0, 3, 0, 3);
        espPortComboBox.Name = "espPortComboBox";
        espPortComboBox.SelectedIndexChanged += control_Edited;
        espPortComboBox.TextChanged += control_Edited;
        espPortComboBox.Size = new Size(220, 25);
        espPortComboBox.TabIndex = 1;
        //
        // espPortHintLabel
        //
        espPortHintLabel.AutoSize = true;
        hardwareLayout.SetColumnSpan(espPortHintLabel, 2);
        espPortHintLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 238);
        espPortHintLabel.ForeColor = Color.FromArgb(96, 106, 120);
        espPortHintLabel.Margin = new Padding(0, 0, 0, 8);
        espPortHintLabel.MaximumSize = new Size(470, 0);
        espPortHintLabel.Name = "espPortHintLabel";
        espPortHintLabel.TabIndex = 2;
        espPortHintLabel.Text = "Automatikus felismerésnél a program az USB-eszközazonosítók alapján keresi meg az ESP-t. A port neve kézzel is beírható, pl. COM5.";
        //
        // baudRateLabel
        //
        baudRateLabel.Anchor = AnchorStyles.Left;
        baudRateLabel.AutoSize = true;
        baudRateLabel.Margin = new Padding(0, 4, 10, 4);
        baudRateLabel.Name = "baudRateLabel";
        baudRateLabel.TabIndex = 3;
        baudRateLabel.Text = "Átviteli sebesség (baud)";
        //
        // baudRateComboBox
        //
        baudRateComboBox.Anchor = AnchorStyles.Left;
        baudRateComboBox.Margin = new Padding(0, 3, 0, 3);
        baudRateComboBox.Name = "baudRateComboBox";
        baudRateComboBox.SelectedIndexChanged += control_Edited;
        baudRateComboBox.TextChanged += control_Edited;
        baudRateComboBox.Size = new Size(140, 25);
        baudRateComboBox.TabIndex = 4;
        baudRateComboBox.KeyPress += baudRateComboBox_KeyPress;
        //
        // baudRateHintLabel
        //
        baudRateHintLabel.AutoSize = true;
        hardwareLayout.SetColumnSpan(baudRateHintLabel, 2);
        baudRateHintLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 238);
        baudRateHintLabel.ForeColor = Color.FromArgb(96, 106, 120);
        baudRateHintLabel.Margin = new Padding(0, 0, 0, 4);
        baudRateHintLabel.MaximumSize = new Size(470, 0);
        baudRateHintLabel.Name = "baudRateHintLabel";
        baudRateHintLabel.TabIndex = 5;
        baudRateHintLabel.Text = "Az ESP firmware-ben beállított sebességgel egyezzen.";
        //
        // photodiodeHeaderLabel
        //
        photodiodeHeaderLabel.AutoSize = true;
        hardwareLayout.SetColumnSpan(photodiodeHeaderLabel, 2);
        photodiodeHeaderLabel.ForeColor = Color.FromArgb(52, 58, 70);
        photodiodeHeaderLabel.Margin = new Padding(0, 14, 0, 4);
        photodiodeHeaderLabel.Name = "photodiodeHeaderLabel";
        photodiodeHeaderLabel.TabIndex = 6;
        photodiodeHeaderLabel.Text = "A fotodióda-jelző helye";
        //
        // photodiodeButtonsPanel
        //
        photodiodeButtonsPanel.AutoSize = true;
        photodiodeButtonsPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        hardwareLayout.SetColumnSpan(photodiodeButtonsPanel, 2);
        photodiodeButtonsPanel.Controls.Add(movePhotodiodeButton);
        photodiodeButtonsPanel.Controls.Add(resetPhotodiodeButton);
        photodiodeButtonsPanel.Margin = new Padding(0, 2, 0, 0);
        photodiodeButtonsPanel.Name = "photodiodeButtonsPanel";
        photodiodeButtonsPanel.TabIndex = 7;
        photodiodeButtonsPanel.WrapContents = false;
        //
        // movePhotodiodeButton
        //
        movePhotodiodeButton.AutoSize = true;
        movePhotodiodeButton.BackColor = Color.FromArgb(244, 238, 237);
        movePhotodiodeButton.Cursor = Cursors.Hand;
        movePhotodiodeButton.FlatAppearance.BorderColor = Color.FromArgb(222, 212, 210);
        movePhotodiodeButton.FlatStyle = FlatStyle.Flat;
        movePhotodiodeButton.ForeColor = Color.FromArgb(85, 94, 114);
        movePhotodiodeButton.Margin = new Padding(0, 0, 8, 0);
        movePhotodiodeButton.MinimumSize = new Size(0, 30);
        movePhotodiodeButton.Name = "movePhotodiodeButton";
        movePhotodiodeButton.Padding = new Padding(6, 0, 6, 0);
        movePhotodiodeButton.TabIndex = 0;
        movePhotodiodeButton.Text = "Áthelyezés a mérőfelületen…";
        movePhotodiodeButton.UseVisualStyleBackColor = false;
        movePhotodiodeButton.Click += movePhotodiodeButton_Click;
        //
        // resetPhotodiodeButton
        //
        resetPhotodiodeButton.AutoSize = true;
        resetPhotodiodeButton.BackColor = Color.FromArgb(244, 238, 237);
        resetPhotodiodeButton.Cursor = Cursors.Hand;
        resetPhotodiodeButton.FlatAppearance.BorderColor = Color.FromArgb(222, 212, 210);
        resetPhotodiodeButton.FlatStyle = FlatStyle.Flat;
        resetPhotodiodeButton.ForeColor = Color.FromArgb(85, 94, 114);
        resetPhotodiodeButton.Margin = new Padding(0, 0, 8, 0);
        resetPhotodiodeButton.MinimumSize = new Size(0, 30);
        resetPhotodiodeButton.Name = "resetPhotodiodeButton";
        resetPhotodiodeButton.Paint += disabledButton_Paint;
        resetPhotodiodeButton.Padding = new Padding(6, 0, 6, 0);
        resetPhotodiodeButton.TabIndex = 1;
        resetPhotodiodeButton.Text = "Alaphelyzet (jobb alsó sarok)";
        resetPhotodiodeButton.UseVisualStyleBackColor = false;
        resetPhotodiodeButton.Click += resetPhotodiodeButton_Click;
        //
        // photodiodePositionLabel
        //
        photodiodePositionLabel.AutoSize = true;
        hardwareLayout.SetColumnSpan(photodiodePositionLabel, 2);
        photodiodePositionLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 238);
        photodiodePositionLabel.ForeColor = Color.FromArgb(64, 70, 82);
        photodiodePositionLabel.Margin = new Padding(0, 6, 0, 4);
        photodiodePositionLabel.MaximumSize = new Size(470, 0);
        photodiodePositionLabel.Name = "photodiodePositionLabel";
        photodiodePositionLabel.TabIndex = 8;
        //
        // photodiodeHintLabel
        //
        photodiodeHintLabel.AutoSize = true;
        hardwareLayout.SetColumnSpan(photodiodeHintLabel, 2);
        photodiodeHintLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 238);
        photodiodeHintLabel.ForeColor = Color.FromArgb(96, 106, 120);
        photodiodeHintLabel.Margin = new Padding(0, 0, 0, 4);
        photodiodeHintLabel.MaximumSize = new Size(470, 0);
        photodiodeHintLabel.Name = "photodiodeHintLabel";
        photodiodeHintLabel.TabIndex = 9;
        photodiodeHintLabel.Text = "Ha a fotodióda nem a képernyő jobb alsó sarkában van, helyezd át a jelzőt pontosan a szenzor alá. "
            + "Az inger és a fixációs kereszt nem kerülhet a jelző közelébe, és a képkirajzolás (scanout) korrekciója is a jelző "
            + "helyével számol.";
        //
        // HardwareSettingsTab
        //
        AutoScaleMode = AutoScaleMode.Inherit;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Color.FromArgb(253, 250, 250);
        Controls.Add(hardwareLayout);
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point, 238);
        Margin = new Padding(0);
        Name = "HardwareSettingsTab";
        Size = new Size(470, 300);
        hardwareLayout.ResumeLayout(false);
        hardwareLayout.PerformLayout();
        photodiodeButtonsPanel.ResumeLayout(false);
        photodiodeButtonsPanel.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion
}
