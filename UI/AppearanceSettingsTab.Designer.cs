namespace ReactionTimeMeasurementApp.UI;

partial class AppearanceSettingsTab
{
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel appearanceLayout;
    private Label stimulusColorLabel;
    private FlowLayoutPanel stimulusColorPanel;
    private Button stimulusColorSwatchButton;
    private TextBox stimulusColorTextBox;
    private Label backgroundColorLabel;
    private FlowLayoutPanel backgroundColorPanel;
    private Button backgroundColorSwatchButton;
    private TextBox backgroundColorTextBox;
    private Button swapColorsButton;
    private Label colorHintLabel;
    private Label diameterLabel;
    private NumericUpDown diameterNumeric;
    private Label diameterUnitLabel;
    private Label diameterHintLabel;
    private Label eccentricityHeaderLabel;
    private Label minEccentricityLabel;
    private NumericUpDown minEccentricityNumeric;
    private Label minEccentricityUnitLabel;
    private Label maxEccentricityLabel;
    private NumericUpDown maxEccentricityNumeric;
    private Label maxEccentricityUnitLabel;
    private Label eccentricityHintLabel;
    private ColorDialog colorDialog;

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
        appearanceLayout = new TableLayoutPanel();
        stimulusColorLabel = new Label();
        stimulusColorPanel = new FlowLayoutPanel();
        stimulusColorSwatchButton = new Button();
        stimulusColorTextBox = new TextBox();
        backgroundColorLabel = new Label();
        backgroundColorPanel = new FlowLayoutPanel();
        backgroundColorSwatchButton = new Button();
        backgroundColorTextBox = new TextBox();
        swapColorsButton = new Button();
        colorHintLabel = new Label();
        diameterLabel = new Label();
        diameterNumeric = new NumericUpDown();
        diameterUnitLabel = new Label();
        diameterHintLabel = new Label();
        eccentricityHeaderLabel = new Label();
        minEccentricityLabel = new Label();
        minEccentricityNumeric = new NumericUpDown();
        minEccentricityUnitLabel = new Label();
        maxEccentricityLabel = new Label();
        maxEccentricityNumeric = new NumericUpDown();
        maxEccentricityUnitLabel = new Label();
        eccentricityHintLabel = new Label();
        colorDialog = new ColorDialog();
        appearanceLayout.SuspendLayout();
        stimulusColorPanel.SuspendLayout();
        backgroundColorPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)diameterNumeric).BeginInit();
        ((System.ComponentModel.ISupportInitialize)minEccentricityNumeric).BeginInit();
        ((System.ComponentModel.ISupportInitialize)maxEccentricityNumeric).BeginInit();
        SuspendLayout();
        // appearanceLayout
        //
        appearanceLayout.AutoSize = true;
        appearanceLayout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        appearanceLayout.ColumnCount = 3;
        appearanceLayout.ColumnStyles.Add(new ColumnStyle());
        appearanceLayout.ColumnStyles.Add(new ColumnStyle());
        appearanceLayout.ColumnStyles.Add(new ColumnStyle());
        appearanceLayout.Controls.Add(stimulusColorLabel, 0, 0);
        appearanceLayout.Controls.Add(stimulusColorPanel, 1, 0);
        appearanceLayout.Controls.Add(backgroundColorLabel, 0, 1);
        appearanceLayout.Controls.Add(backgroundColorPanel, 1, 1);
        appearanceLayout.Controls.Add(swapColorsButton, 1, 2);
        appearanceLayout.Controls.Add(colorHintLabel, 0, 3);
        appearanceLayout.Controls.Add(diameterLabel, 0, 4);
        appearanceLayout.Controls.Add(diameterNumeric, 1, 4);
        appearanceLayout.Controls.Add(diameterUnitLabel, 2, 4);
        appearanceLayout.Controls.Add(diameterHintLabel, 0, 5);
        appearanceLayout.Controls.Add(eccentricityHeaderLabel, 0, 6);
        appearanceLayout.Controls.Add(minEccentricityLabel, 0, 7);
        appearanceLayout.Controls.Add(minEccentricityNumeric, 1, 7);
        appearanceLayout.Controls.Add(minEccentricityUnitLabel, 2, 7);
        appearanceLayout.Controls.Add(maxEccentricityLabel, 0, 8);
        appearanceLayout.Controls.Add(maxEccentricityNumeric, 1, 8);
        appearanceLayout.Controls.Add(maxEccentricityUnitLabel, 2, 8);
        appearanceLayout.Controls.Add(eccentricityHintLabel, 0, 9);
        appearanceLayout.Location = new Point(0, 0);
        appearanceLayout.Margin = new Padding(0);
        appearanceLayout.Name = "appearanceLayout";
        appearanceLayout.RowCount = 10;
        appearanceLayout.RowStyles.Add(new RowStyle());
        appearanceLayout.RowStyles.Add(new RowStyle());
        appearanceLayout.RowStyles.Add(new RowStyle());
        appearanceLayout.RowStyles.Add(new RowStyle());
        appearanceLayout.RowStyles.Add(new RowStyle());
        appearanceLayout.RowStyles.Add(new RowStyle());
        appearanceLayout.RowStyles.Add(new RowStyle());
        appearanceLayout.RowStyles.Add(new RowStyle());
        appearanceLayout.RowStyles.Add(new RowStyle());
        appearanceLayout.RowStyles.Add(new RowStyle());
        appearanceLayout.Size = new Size(470, 360);
        appearanceLayout.TabIndex = 0;
        //
        // stimulusColorLabel
        //
        stimulusColorLabel.Anchor = AnchorStyles.Left;
        stimulusColorLabel.AutoSize = true;
        stimulusColorLabel.Margin = new Padding(0, 4, 10, 4);
        stimulusColorLabel.Name = "stimulusColorLabel";
        stimulusColorLabel.TabIndex = 0;
        stimulusColorLabel.Text = "Inger színe";
        //
        // stimulusColorPanel
        //
        stimulusColorPanel.Anchor = AnchorStyles.Left;
        stimulusColorPanel.AutoSize = true;
        stimulusColorPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        appearanceLayout.SetColumnSpan(stimulusColorPanel, 2);
        stimulusColorPanel.Controls.Add(stimulusColorSwatchButton);
        stimulusColorPanel.Controls.Add(stimulusColorTextBox);
        stimulusColorPanel.Margin = new Padding(0, 2, 0, 2);
        stimulusColorPanel.Name = "stimulusColorPanel";
        stimulusColorPanel.TabIndex = 1;
        stimulusColorPanel.WrapContents = false;
        //
        // stimulusColorSwatchButton
        //
        stimulusColorSwatchButton.Anchor = AnchorStyles.Left;
        stimulusColorSwatchButton.Cursor = Cursors.Hand;
        stimulusColorSwatchButton.FlatAppearance.BorderColor = Color.FromArgb(150, 150, 150);
        stimulusColorSwatchButton.FlatStyle = FlatStyle.Flat;
        stimulusColorSwatchButton.Margin = new Padding(0, 1, 8, 1);
        stimulusColorSwatchButton.Name = "stimulusColorSwatchButton";
        stimulusColorSwatchButton.Size = new Size(40, 26);
        stimulusColorSwatchButton.TabIndex = 0;
        stimulusColorSwatchButton.UseVisualStyleBackColor = false;
        stimulusColorSwatchButton.Click += stimulusColorSwatchButton_Click;
        //
        // stimulusColorTextBox
        //
        stimulusColorTextBox.Anchor = AnchorStyles.Left;
        stimulusColorTextBox.CharacterCasing = CharacterCasing.Upper;
        stimulusColorTextBox.Margin = new Padding(0, 1, 0, 1);
        stimulusColorTextBox.MaxLength = 16;
        stimulusColorTextBox.Name = "stimulusColorTextBox";
        stimulusColorTextBox.TextChanged += colorTextBox_TextChanged;
        stimulusColorTextBox.Size = new Size(96, 25);
        stimulusColorTextBox.TabIndex = 1;
        stimulusColorTextBox.Validated += colorTextBox_Validated;
        //
        // backgroundColorLabel
        //
        backgroundColorLabel.Anchor = AnchorStyles.Left;
        backgroundColorLabel.AutoSize = true;
        backgroundColorLabel.Margin = new Padding(0, 4, 10, 4);
        backgroundColorLabel.Name = "backgroundColorLabel";
        backgroundColorLabel.TabIndex = 2;
        backgroundColorLabel.Text = "Háttérszín";
        //
        // backgroundColorPanel
        //
        backgroundColorPanel.Anchor = AnchorStyles.Left;
        backgroundColorPanel.AutoSize = true;
        backgroundColorPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        appearanceLayout.SetColumnSpan(backgroundColorPanel, 2);
        backgroundColorPanel.Controls.Add(backgroundColorSwatchButton);
        backgroundColorPanel.Controls.Add(backgroundColorTextBox);
        backgroundColorPanel.Margin = new Padding(0, 2, 0, 2);
        backgroundColorPanel.Name = "backgroundColorPanel";
        backgroundColorPanel.TabIndex = 3;
        backgroundColorPanel.WrapContents = false;
        //
        // backgroundColorSwatchButton
        //
        backgroundColorSwatchButton.Anchor = AnchorStyles.Left;
        backgroundColorSwatchButton.Cursor = Cursors.Hand;
        backgroundColorSwatchButton.FlatAppearance.BorderColor = Color.FromArgb(150, 150, 150);
        backgroundColorSwatchButton.FlatStyle = FlatStyle.Flat;
        backgroundColorSwatchButton.Margin = new Padding(0, 1, 8, 1);
        backgroundColorSwatchButton.Name = "backgroundColorSwatchButton";
        backgroundColorSwatchButton.Size = new Size(40, 26);
        backgroundColorSwatchButton.TabIndex = 0;
        backgroundColorSwatchButton.UseVisualStyleBackColor = false;
        backgroundColorSwatchButton.Click += backgroundColorSwatchButton_Click;
        //
        // backgroundColorTextBox
        //
        backgroundColorTextBox.Anchor = AnchorStyles.Left;
        backgroundColorTextBox.CharacterCasing = CharacterCasing.Upper;
        backgroundColorTextBox.Margin = new Padding(0, 1, 0, 1);
        backgroundColorTextBox.MaxLength = 16;
        backgroundColorTextBox.Name = "backgroundColorTextBox";
        backgroundColorTextBox.TextChanged += colorTextBox_TextChanged;
        backgroundColorTextBox.Size = new Size(96, 25);
        backgroundColorTextBox.TabIndex = 1;
        backgroundColorTextBox.Validated += colorTextBox_Validated;
        //
        // swapColorsButton
        //
        swapColorsButton.Anchor = AnchorStyles.Left;
        swapColorsButton.AutoSize = true;
        swapColorsButton.BackColor = Color.FromArgb(244, 238, 237);
        appearanceLayout.SetColumnSpan(swapColorsButton, 2);
        swapColorsButton.Cursor = Cursors.Hand;
        swapColorsButton.FlatAppearance.BorderColor = Color.FromArgb(222, 212, 210);
        swapColorsButton.FlatStyle = FlatStyle.Flat;
        swapColorsButton.ForeColor = Color.FromArgb(85, 94, 114);
        swapColorsButton.Margin = new Padding(0, 3, 0, 3);
        swapColorsButton.MinimumSize = new Size(0, 30);
        swapColorsButton.Name = "swapColorsButton";
        swapColorsButton.Padding = new Padding(6, 0, 6, 0);
        swapColorsButton.TabIndex = 4;
        swapColorsButton.Text = "⇄ Színcsere";
        swapColorsButton.UseVisualStyleBackColor = false;
        swapColorsButton.Click += swapColorsButton_Click;
        //
        // colorHintLabel
        //
        colorHintLabel.AutoSize = true;
        appearanceLayout.SetColumnSpan(colorHintLabel, 3);
        colorHintLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 238);
        colorHintLabel.ForeColor = Color.FromArgb(96, 106, 120);
        colorHintLabel.Margin = new Padding(0, 2, 0, 8);
        colorHintLabel.MaximumSize = new Size(470, 0);
        colorHintLabel.Name = "colorHintLabel";
        colorHintLabel.TabIndex = 5;
        colorHintLabel.Text = "#RRGGBB alakban; a beírt érték a mező elhagyásakor vagy Enterre érvényesül. A beállított háttér csak mérés közben látszik, a mérés előtti képernyőn nem.";
        //
        // diameterLabel
        //
        diameterLabel.Anchor = AnchorStyles.Left;
        diameterLabel.AutoSize = true;
        diameterLabel.Margin = new Padding(0, 4, 10, 4);
        diameterLabel.Name = "diameterLabel";
        diameterLabel.TabIndex = 6;
        diameterLabel.Text = "Inger mérete (átmérő)";
        //
        // diameterNumeric
        //
        diameterNumeric.Anchor = AnchorStyles.Left;
        diameterNumeric.Margin = new Padding(0, 3, 6, 3);
        diameterNumeric.Name = "diameterNumeric";
        diameterNumeric.Size = new Size(96, 25);
        diameterNumeric.TabIndex = 7;
        diameterNumeric.TextAlign = HorizontalAlignment.Right;
        //
        // diameterUnitLabel
        //
        diameterUnitLabel.Anchor = AnchorStyles.Left;
        diameterUnitLabel.AutoSize = true;
        diameterUnitLabel.Margin = new Padding(0, 4, 0, 4);
        diameterUnitLabel.Name = "diameterUnitLabel";
        diameterUnitLabel.TabIndex = 8;
        diameterUnitLabel.Text = "px";
        //
        // diameterHintLabel
        //
        diameterHintLabel.AutoSize = true;
        appearanceLayout.SetColumnSpan(diameterHintLabel, 3);
        diameterHintLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 238);
        diameterHintLabel.ForeColor = Color.FromArgb(96, 106, 120);
        diameterHintLabel.Margin = new Padding(0, 0, 0, 6);
        diameterHintLabel.MaximumSize = new Size(470, 0);
        diameterHintLabel.Name = "diameterHintLabel";
        diameterHintLabel.TabIndex = 9;
        diameterHintLabel.Text = "Képpontban; a fizikai méret a monitortól függ. A valós méretet a jobb oldali alsó keret mutatja.";
        //
        // eccentricityHeaderLabel
        //
        eccentricityHeaderLabel.AutoSize = true;
        appearanceLayout.SetColumnSpan(eccentricityHeaderLabel, 3);
        eccentricityHeaderLabel.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Regular, GraphicsUnit.Point, 238);
        eccentricityHeaderLabel.ForeColor = Color.FromArgb(52, 58, 70);
        eccentricityHeaderLabel.Margin = new Padding(0, 6, 0, 2);
        eccentricityHeaderLabel.Name = "eccentricityHeaderLabel";
        eccentricityHeaderLabel.TabIndex = 10;
        eccentricityHeaderLabel.Text = "Az inger távolsága a fixációs ponttól:";
        //
        // minEccentricityLabel
        //
        minEccentricityLabel.Anchor = AnchorStyles.Left;
        minEccentricityLabel.AutoSize = true;
        minEccentricityLabel.Margin = new Padding(18, 4, 10, 4);
        minEccentricityLabel.Name = "minEccentricityLabel";
        minEccentricityLabel.TabIndex = 11;
        minEccentricityLabel.Text = "legalább";
        //
        // minEccentricityNumeric
        //
        minEccentricityNumeric.Anchor = AnchorStyles.Left;
        minEccentricityNumeric.Margin = new Padding(0, 3, 6, 3);
        minEccentricityNumeric.Name = "minEccentricityNumeric";
        minEccentricityNumeric.Size = new Size(96, 25);
        minEccentricityNumeric.TabIndex = 12;
        minEccentricityNumeric.TextAlign = HorizontalAlignment.Right;
        //
        // minEccentricityUnitLabel
        //
        minEccentricityUnitLabel.Anchor = AnchorStyles.Left;
        minEccentricityUnitLabel.AutoSize = true;
        minEccentricityUnitLabel.Margin = new Padding(0, 4, 0, 4);
        minEccentricityUnitLabel.Name = "minEccentricityUnitLabel";
        minEccentricityUnitLabel.TabIndex = 13;
        minEccentricityUnitLabel.Text = "px";
        //
        // maxEccentricityLabel
        //
        maxEccentricityLabel.Anchor = AnchorStyles.Left;
        maxEccentricityLabel.AutoSize = true;
        maxEccentricityLabel.Margin = new Padding(18, 4, 10, 4);
        maxEccentricityLabel.Name = "maxEccentricityLabel";
        maxEccentricityLabel.TabIndex = 14;
        maxEccentricityLabel.Text = "legfeljebb";
        //
        // maxEccentricityNumeric
        //
        maxEccentricityNumeric.Anchor = AnchorStyles.Left;
        maxEccentricityNumeric.Margin = new Padding(0, 3, 6, 3);
        maxEccentricityNumeric.Name = "maxEccentricityNumeric";
        maxEccentricityNumeric.Size = new Size(96, 25);
        maxEccentricityNumeric.TabIndex = 15;
        maxEccentricityNumeric.TextAlign = HorizontalAlignment.Right;
        //
        // maxEccentricityUnitLabel
        //
        maxEccentricityUnitLabel.Anchor = AnchorStyles.Left;
        maxEccentricityUnitLabel.AutoSize = true;
        maxEccentricityUnitLabel.Margin = new Padding(0, 4, 0, 4);
        maxEccentricityUnitLabel.Name = "maxEccentricityUnitLabel";
        maxEccentricityUnitLabel.TabIndex = 16;
        maxEccentricityUnitLabel.Text = "px";
        //
        // eccentricityHintLabel
        //
        eccentricityHintLabel.AutoSize = true;
        appearanceLayout.SetColumnSpan(eccentricityHintLabel, 3);
        eccentricityHintLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 238);
        eccentricityHintLabel.ForeColor = Color.FromArgb(96, 106, 120);
        eccentricityHintLabel.Margin = new Padding(0, 2, 0, 4);
        eccentricityHintLabel.MaximumSize = new Size(470, 0);
        eccentricityHintLabel.Name = "eccentricityHintLabel";
        eccentricityHintLabel.TabIndex = 17;
        eccentricityHintLabel.Text = "Az inger középpontjának távolsága. Próbánként véletlen érték a két határ között (egyenletes eloszlással), az irány a képernyőn elférő irányok közül véletlenszerű.";
        //
        // colorDialog
        //
        colorDialog.AnyColor = true;
        colorDialog.FullOpen = true;
        //
        // AppearanceSettingsTab
        //
        AutoScaleMode = AutoScaleMode.Inherit;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Color.FromArgb(253, 250, 250);
        Controls.Add(appearanceLayout);
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point, 238);
        Margin = new Padding(0);
        Name = "AppearanceSettingsTab";
        Size = new Size(470, 360);
        appearanceLayout.ResumeLayout(false);
        appearanceLayout.PerformLayout();
        stimulusColorPanel.ResumeLayout(false);
        stimulusColorPanel.PerformLayout();
        backgroundColorPanel.ResumeLayout(false);
        backgroundColorPanel.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)diameterNumeric).EndInit();
        ((System.ComponentModel.ISupportInitialize)minEccentricityNumeric).EndInit();
        ((System.ComponentModel.ISupportInitialize)maxEccentricityNumeric).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion
}
