namespace ReactionTimeMeasurementApp.UI;

partial class GazeSettingsTab
{
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel gazeLayout;
    private CheckBox gazeValidationCheckBox;
    private Label gazeValidationHintLabel;
    private Label gazeTargetHeaderLabel;
    private FlowLayoutPanel gazeTargetPanel;
    private RadioButton lookAtStimulusRadioButton;
    private RadioButton keepFixationRadioButton;
    private Label fixationPlacementHeaderLabel;
    private TableLayoutPanel fixationPlacementPanel;
    private RadioButton centerPlacementRadioButton;
    private RadioButton customPlacementRadioButton;
    private Button pickFixationPointButton;
    private Label fixationPointLabel;
    private Label validityRadiusLabel;
    private NumericUpDown validityRadiusNumeric;
    private Label validityRadiusUnitLabel;
    private Label validityRadiusHintLabel;
    private Label minGazeRatioLabel;
    private NumericUpDown minGazeRatioNumeric;
    private Label minGazeRatioUnitLabel;
    private Label minGazeRatioHintLabel;

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
        gazeLayout = new TableLayoutPanel();
        gazeValidationCheckBox = new CheckBox();
        gazeValidationHintLabel = new Label();
        gazeTargetHeaderLabel = new Label();
        gazeTargetPanel = new FlowLayoutPanel();
        lookAtStimulusRadioButton = new RadioButton();
        keepFixationRadioButton = new RadioButton();
        fixationPlacementHeaderLabel = new Label();
        fixationPlacementPanel = new TableLayoutPanel();
        centerPlacementRadioButton = new RadioButton();
        customPlacementRadioButton = new RadioButton();
        pickFixationPointButton = new Button();
        fixationPointLabel = new Label();
        validityRadiusLabel = new Label();
        validityRadiusNumeric = new NumericUpDown();
        validityRadiusUnitLabel = new Label();
        validityRadiusHintLabel = new Label();
        minGazeRatioLabel = new Label();
        minGazeRatioNumeric = new NumericUpDown();
        minGazeRatioUnitLabel = new Label();
        minGazeRatioHintLabel = new Label();
        gazeLayout.SuspendLayout();
        gazeTargetPanel.SuspendLayout();
        fixationPlacementPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)validityRadiusNumeric).BeginInit();
        ((System.ComponentModel.ISupportInitialize)minGazeRatioNumeric).BeginInit();
        SuspendLayout();
        // gazeLayout
        //
        gazeLayout.AutoSize = true;
        gazeLayout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        gazeLayout.ColumnCount = 3;
        gazeLayout.ColumnStyles.Add(new ColumnStyle());
        gazeLayout.ColumnStyles.Add(new ColumnStyle());
        gazeLayout.ColumnStyles.Add(new ColumnStyle());
        gazeLayout.Controls.Add(gazeValidationCheckBox, 0, 0);
        gazeLayout.Controls.Add(gazeValidationHintLabel, 0, 1);
        gazeLayout.Controls.Add(gazeTargetHeaderLabel, 0, 2);
        gazeLayout.Controls.Add(gazeTargetPanel, 0, 3);
        gazeLayout.Controls.Add(fixationPlacementHeaderLabel, 0, 4);
        gazeLayout.Controls.Add(fixationPlacementPanel, 0, 5);
        gazeLayout.Controls.Add(validityRadiusLabel, 0, 6);
        gazeLayout.Controls.Add(validityRadiusNumeric, 1, 6);
        gazeLayout.Controls.Add(validityRadiusUnitLabel, 2, 6);
        gazeLayout.Controls.Add(validityRadiusHintLabel, 0, 7);
        gazeLayout.Controls.Add(minGazeRatioLabel, 0, 8);
        gazeLayout.Controls.Add(minGazeRatioNumeric, 1, 8);
        gazeLayout.Controls.Add(minGazeRatioUnitLabel, 2, 8);
        gazeLayout.Controls.Add(minGazeRatioHintLabel, 0, 9);
        gazeLayout.Location = new Point(0, 0);
        gazeLayout.Margin = new Padding(0);
        gazeLayout.Name = "gazeLayout";
        gazeLayout.RowCount = 10;
        gazeLayout.RowStyles.Add(new RowStyle());
        gazeLayout.RowStyles.Add(new RowStyle());
        gazeLayout.RowStyles.Add(new RowStyle());
        gazeLayout.RowStyles.Add(new RowStyle());
        gazeLayout.RowStyles.Add(new RowStyle());
        gazeLayout.RowStyles.Add(new RowStyle());
        gazeLayout.RowStyles.Add(new RowStyle());
        gazeLayout.RowStyles.Add(new RowStyle());
        gazeLayout.RowStyles.Add(new RowStyle());
        gazeLayout.RowStyles.Add(new RowStyle());
        gazeLayout.Size = new Size(470, 380);
        gazeLayout.TabIndex = 0;
        //
        // gazeValidationCheckBox
        //
        gazeValidationCheckBox.AutoSize = true;
        gazeLayout.SetColumnSpan(gazeValidationCheckBox, 3);
        gazeValidationCheckBox.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Regular, GraphicsUnit.Point, 238);
        gazeValidationCheckBox.ForeColor = Color.FromArgb(52, 58, 70);
        gazeValidationCheckBox.Margin = new Padding(0, 2, 0, 0);
        gazeValidationCheckBox.Name = "gazeValidationCheckBox";
        gazeValidationCheckBox.TabIndex = 0;
        gazeValidationCheckBox.Text = "Tekintet-ellenőrzés szükséges";
        gazeValidationCheckBox.UseVisualStyleBackColor = true;
        gazeValidationCheckBox.CheckedChanged += gazeOption_CheckedChanged;
        //
        // gazeValidationHintLabel
        //
        gazeValidationHintLabel.AutoSize = true;
        gazeLayout.SetColumnSpan(gazeValidationHintLabel, 3);
        gazeValidationHintLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 238);
        gazeValidationHintLabel.ForeColor = Color.FromArgb(96, 106, 120);
        gazeValidationHintLabel.Margin = new Padding(18, 0, 0, 4);
        gazeValidationHintLabel.MaximumSize = new Size(452, 0);
        gazeValidationHintLabel.Name = "gazeValidationHintLabel";
        gazeValidationHintLabel.TabIndex = 1;
        gazeValidationHintLabel.Text = "Kikapcsolva nem kell szemkövető és kalibráció: minden, a válaszidő-korláton belül érkező válasz érvényes.";
        //
        // gazeTargetHeaderLabel
        //
        gazeTargetHeaderLabel.AutoSize = true;
        gazeLayout.SetColumnSpan(gazeTargetHeaderLabel, 3);
        gazeTargetHeaderLabel.ForeColor = Color.FromArgb(52, 58, 70);
        gazeTargetHeaderLabel.Margin = new Padding(18, 4, 0, 0);
        gazeTargetHeaderLabel.Name = "gazeTargetHeaderLabel";
        gazeTargetHeaderLabel.TabIndex = 2;
        gazeTargetHeaderLabel.Text = "Mit kell néznie a résztvevőnek?";
        //
        // gazeTargetPanel
        //
        gazeTargetPanel.AutoSize = true;
        gazeTargetPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        gazeLayout.SetColumnSpan(gazeTargetPanel, 3);
        gazeTargetPanel.Controls.Add(lookAtStimulusRadioButton);
        gazeTargetPanel.Controls.Add(keepFixationRadioButton);
        gazeTargetPanel.FlowDirection = FlowDirection.TopDown;
        gazeTargetPanel.Margin = new Padding(36, 0, 0, 2);
        gazeTargetPanel.Name = "gazeTargetPanel";
        gazeTargetPanel.TabIndex = 3;
        gazeTargetPanel.WrapContents = false;
        //
        // lookAtStimulusRadioButton
        //
        lookAtStimulusRadioButton.AutoSize = true;
        lookAtStimulusRadioButton.Margin = new Padding(0, 2, 0, 0);
        lookAtStimulusRadioButton.Name = "lookAtStimulusRadioButton";
        lookAtStimulusRadioButton.TabIndex = 0;
        lookAtStimulusRadioButton.TabStop = true;
        lookAtStimulusRadioButton.Text = "Az ingert (normál reakcióidő)";
        lookAtStimulusRadioButton.UseVisualStyleBackColor = true;
        lookAtStimulusRadioButton.CheckedChanged += gazeOption_CheckedChanged;
        //
        // keepFixationRadioButton
        //
        keepFixationRadioButton.AutoSize = true;
        keepFixationRadioButton.Margin = new Padding(0, 2, 0, 0);
        keepFixationRadioButton.Name = "keepFixationRadioButton";
        keepFixationRadioButton.TabIndex = 1;
        keepFixationRadioButton.Text = "A fixációs pontot (periférikus reakcióidő)";
        keepFixationRadioButton.UseVisualStyleBackColor = true;
        keepFixationRadioButton.CheckedChanged += gazeOption_CheckedChanged;
        //
        // fixationPlacementHeaderLabel
        //
        fixationPlacementHeaderLabel.AutoSize = true;
        gazeLayout.SetColumnSpan(fixationPlacementHeaderLabel, 3);
        fixationPlacementHeaderLabel.ForeColor = Color.FromArgb(52, 58, 70);
        fixationPlacementHeaderLabel.Margin = new Padding(18, 4, 0, 0);
        fixationPlacementHeaderLabel.Name = "fixationPlacementHeaderLabel";
        fixationPlacementHeaderLabel.TabIndex = 4;
        fixationPlacementHeaderLabel.Text = "A fixációs pont helye";
        //
        // fixationPlacementPanel
        //
        fixationPlacementPanel.AutoSize = true;
        fixationPlacementPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        fixationPlacementPanel.ColumnCount = 2;
        fixationPlacementPanel.ColumnStyles.Add(new ColumnStyle());
        fixationPlacementPanel.ColumnStyles.Add(new ColumnStyle());
        gazeLayout.SetColumnSpan(fixationPlacementPanel, 3);
        fixationPlacementPanel.Controls.Add(centerPlacementRadioButton, 0, 0);
        fixationPlacementPanel.Controls.Add(customPlacementRadioButton, 0, 1);
        fixationPlacementPanel.Controls.Add(pickFixationPointButton, 1, 1);
        fixationPlacementPanel.Controls.Add(fixationPointLabel, 0, 2);
        fixationPlacementPanel.Margin = new Padding(36, 0, 0, 4);
        fixationPlacementPanel.Name = "fixationPlacementPanel";
        fixationPlacementPanel.RowCount = 3;
        fixationPlacementPanel.RowStyles.Add(new RowStyle());
        fixationPlacementPanel.RowStyles.Add(new RowStyle());
        fixationPlacementPanel.RowStyles.Add(new RowStyle());
        fixationPlacementPanel.TabIndex = 5;
        //
        // centerPlacementRadioButton
        //
        centerPlacementRadioButton.AutoSize = true;
        fixationPlacementPanel.SetColumnSpan(centerPlacementRadioButton, 2);
        centerPlacementRadioButton.Margin = new Padding(0, 2, 0, 0);
        centerPlacementRadioButton.Name = "centerPlacementRadioButton";
        centerPlacementRadioButton.TabIndex = 0;
        centerPlacementRadioButton.TabStop = true;
        centerPlacementRadioButton.Text = "Középen";
        centerPlacementRadioButton.UseVisualStyleBackColor = true;
        centerPlacementRadioButton.CheckedChanged += gazeOption_CheckedChanged;
        //
        // customPlacementRadioButton
        //
        customPlacementRadioButton.Anchor = AnchorStyles.Left;
        customPlacementRadioButton.AutoSize = true;
        customPlacementRadioButton.Margin = new Padding(0, 2, 12, 0);
        customPlacementRadioButton.Name = "customPlacementRadioButton";
        customPlacementRadioButton.TabIndex = 1;
        customPlacementRadioButton.Text = "Egyéni hely";
        customPlacementRadioButton.UseVisualStyleBackColor = true;
        customPlacementRadioButton.CheckedChanged += customPlacementRadioButton_CheckedChanged;
        //
        // pickFixationPointButton
        //
        pickFixationPointButton.Anchor = AnchorStyles.Left;
        pickFixationPointButton.AutoSize = true;
        pickFixationPointButton.BackColor = Color.FromArgb(244, 238, 237);
        pickFixationPointButton.Cursor = Cursors.Hand;
        pickFixationPointButton.FlatAppearance.BorderColor = Color.FromArgb(222, 212, 210);
        pickFixationPointButton.FlatStyle = FlatStyle.Flat;
        pickFixationPointButton.ForeColor = Color.FromArgb(85, 94, 114);
        pickFixationPointButton.Margin = new Padding(0, 2, 0, 0);
        pickFixationPointButton.MinimumSize = new Size(0, 30);
        pickFixationPointButton.Name = "pickFixationPointButton";
        pickFixationPointButton.Paint += disabledButton_Paint;
        pickFixationPointButton.Padding = new Padding(6, 0, 6, 0);
        pickFixationPointButton.TabIndex = 2;
        pickFixationPointButton.Text = "Kijelölés a mérőfelületen…";
        pickFixationPointButton.UseVisualStyleBackColor = false;
        pickFixationPointButton.Click += pickFixationPointButton_Click;
        //
        // fixationPointLabel
        //
        fixationPointLabel.AutoSize = true;
        fixationPlacementPanel.SetColumnSpan(fixationPointLabel, 2);
        fixationPointLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 238);
        fixationPointLabel.ForeColor = Color.FromArgb(64, 70, 82);
        fixationPointLabel.Margin = new Padding(20, 2, 0, 0);
        fixationPointLabel.Name = "fixationPointLabel";
        fixationPointLabel.TabIndex = 3;
        //
        // validityRadiusLabel
        //
        validityRadiusLabel.Anchor = AnchorStyles.Left;
        validityRadiusLabel.AutoSize = true;
        validityRadiusLabel.Margin = new Padding(0, 4, 10, 4);
        validityRadiusLabel.Name = "validityRadiusLabel";
        validityRadiusLabel.TabIndex = 6;
        validityRadiusLabel.Text = "Elfogadási rádiusz";
        //
        // validityRadiusNumeric
        //
        validityRadiusNumeric.Anchor = AnchorStyles.Left;
        validityRadiusNumeric.Margin = new Padding(0, 3, 6, 3);
        validityRadiusNumeric.Name = "validityRadiusNumeric";
        validityRadiusNumeric.Size = new Size(96, 25);
        validityRadiusNumeric.TabIndex = 7;
        validityRadiusNumeric.TextAlign = HorizontalAlignment.Right;
        //
        // validityRadiusUnitLabel
        //
        validityRadiusUnitLabel.Anchor = AnchorStyles.Left;
        validityRadiusUnitLabel.AutoSize = true;
        validityRadiusUnitLabel.Margin = new Padding(0, 4, 0, 4);
        validityRadiusUnitLabel.Name = "validityRadiusUnitLabel";
        validityRadiusUnitLabel.TabIndex = 8;
        validityRadiusUnitLabel.Text = "px";
        //
        // validityRadiusHintLabel
        //
        validityRadiusHintLabel.AutoSize = true;
        gazeLayout.SetColumnSpan(validityRadiusHintLabel, 3);
        validityRadiusHintLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 238);
        validityRadiusHintLabel.ForeColor = Color.FromArgb(96, 106, 120);
        validityRadiusHintLabel.Margin = new Padding(0, 0, 0, 6);
        validityRadiusHintLabel.MaximumSize = new Size(470, 0);
        validityRadiusHintLabel.Name = "validityRadiusHintLabel";
        validityRadiusHintLabel.TabIndex = 9;
        //
        // minGazeRatioLabel
        //
        minGazeRatioLabel.Anchor = AnchorStyles.Left;
        minGazeRatioLabel.AutoSize = true;
        minGazeRatioLabel.Margin = new Padding(0, 4, 10, 4);
        minGazeRatioLabel.Name = "minGazeRatioLabel";
        minGazeRatioLabel.TabIndex = 10;
        minGazeRatioLabel.Text = "Érvényes tekintetminták minimális aránya";
        //
        // minGazeRatioNumeric
        //
        minGazeRatioNumeric.Anchor = AnchorStyles.Left;
        minGazeRatioNumeric.Margin = new Padding(0, 3, 6, 3);
        minGazeRatioNumeric.Name = "minGazeRatioNumeric";
        minGazeRatioNumeric.Size = new Size(96, 25);
        minGazeRatioNumeric.TabIndex = 11;
        minGazeRatioNumeric.TextAlign = HorizontalAlignment.Right;
        //
        // minGazeRatioUnitLabel
        //
        minGazeRatioUnitLabel.Anchor = AnchorStyles.Left;
        minGazeRatioUnitLabel.AutoSize = true;
        minGazeRatioUnitLabel.Margin = new Padding(0, 4, 0, 4);
        minGazeRatioUnitLabel.Name = "minGazeRatioUnitLabel";
        minGazeRatioUnitLabel.TabIndex = 12;
        minGazeRatioUnitLabel.Text = "%";
        //
        // minGazeRatioHintLabel
        //
        minGazeRatioHintLabel.AutoSize = true;
        gazeLayout.SetColumnSpan(minGazeRatioHintLabel, 3);
        minGazeRatioHintLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 238);
        minGazeRatioHintLabel.ForeColor = Color.FromArgb(96, 106, 120);
        minGazeRatioHintLabel.Margin = new Padding(0, 0, 0, 4);
        minGazeRatioHintLabel.MaximumSize = new Size(470, 0);
        minGazeRatioHintLabel.Name = "minGazeRatioHintLabel";
        minGazeRatioHintLabel.TabIndex = 13;
        minGazeRatioHintLabel.Text = "Ha a válaszablak tekintetmintáinak ennél kisebb része érvényes (pl. pislogás miatt), a próba érvénytelen.";
        //
        // GazeSettingsTab
        //
        AutoScaleMode = AutoScaleMode.Inherit;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Color.FromArgb(253, 250, 250);
        Controls.Add(gazeLayout);
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point, 238);
        Margin = new Padding(0);
        Name = "GazeSettingsTab";
        Size = new Size(470, 380);
        gazeLayout.ResumeLayout(false);
        gazeLayout.PerformLayout();
        gazeTargetPanel.ResumeLayout(false);
        gazeTargetPanel.PerformLayout();
        fixationPlacementPanel.ResumeLayout(false);
        fixationPlacementPanel.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)validityRadiusNumeric).EndInit();
        ((System.ComponentModel.ISupportInitialize)minGazeRatioNumeric).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion
}
