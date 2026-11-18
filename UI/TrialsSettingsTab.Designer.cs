namespace ReactionTimeMeasurementApp.UI;

partial class TrialsSettingsTab
{
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel trialsLayout;
    private Label responseModeLabel;
    private ComboBox responseModeComboBox;
    private Label responseModeHintLabel;
    private Label measuredTrialsLabel;
    private NumericUpDown measuredTrialsNumeric;
    private Label practiceTrialsLabel;
    private NumericUpDown practiceTrialsNumeric;
    private Label trialCountNoteLabel;
    private Label reactionTimeoutLabel;
    private NumericUpDown reactionTimeoutNumeric;
    private Label reactionTimeoutUnitLabel;
    private Label reactionTimeoutHintLabel;
    private Label foreperiodHeaderLabel;
    private Label foreperiodDistributionLabel;
    private ComboBox foreperiodDistributionComboBox;
    private Label foreperiodMinLabel;
    private NumericUpDown foreperiodMinNumeric;
    private Label foreperiodMinUnitLabel;
    private Label foreperiodMaxLabel;
    private NumericUpDown foreperiodMaxNumeric;
    private Label foreperiodMaxUnitLabel;
    private Label foreperiodMeanLabel;
    private NumericUpDown foreperiodMeanNumeric;
    private Label foreperiodMeanUnitLabel;
    private Label sceneSettleDelayLabel;
    private NumericUpDown sceneSettleDelayNumeric;
    private Label sceneSettleDelayUnitLabel;
    private Label sceneSettleDelayHintLabel;

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
        trialsLayout = new TableLayoutPanel();
        responseModeLabel = new Label();
        responseModeComboBox = new ComboBox();
        responseModeHintLabel = new Label();
        measuredTrialsLabel = new Label();
        measuredTrialsNumeric = new NumericUpDown();
        practiceTrialsLabel = new Label();
        practiceTrialsNumeric = new NumericUpDown();
        trialCountNoteLabel = new Label();
        reactionTimeoutLabel = new Label();
        reactionTimeoutNumeric = new NumericUpDown();
        reactionTimeoutUnitLabel = new Label();
        reactionTimeoutHintLabel = new Label();
        foreperiodHeaderLabel = new Label();
        foreperiodDistributionLabel = new Label();
        foreperiodDistributionComboBox = new ComboBox();
        foreperiodMinLabel = new Label();
        foreperiodMinNumeric = new NumericUpDown();
        foreperiodMinUnitLabel = new Label();
        foreperiodMaxLabel = new Label();
        foreperiodMaxNumeric = new NumericUpDown();
        foreperiodMaxUnitLabel = new Label();
        foreperiodMeanLabel = new Label();
        foreperiodMeanNumeric = new NumericUpDown();
        foreperiodMeanUnitLabel = new Label();
        sceneSettleDelayLabel = new Label();
        sceneSettleDelayNumeric = new NumericUpDown();
        sceneSettleDelayUnitLabel = new Label();
        sceneSettleDelayHintLabel = new Label();
        trialsLayout.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)measuredTrialsNumeric).BeginInit();
        ((System.ComponentModel.ISupportInitialize)practiceTrialsNumeric).BeginInit();
        ((System.ComponentModel.ISupportInitialize)reactionTimeoutNumeric).BeginInit();
        ((System.ComponentModel.ISupportInitialize)foreperiodMinNumeric).BeginInit();
        ((System.ComponentModel.ISupportInitialize)foreperiodMaxNumeric).BeginInit();
        ((System.ComponentModel.ISupportInitialize)foreperiodMeanNumeric).BeginInit();
        ((System.ComponentModel.ISupportInitialize)sceneSettleDelayNumeric).BeginInit();
        SuspendLayout();
        // trialsLayout
        //
        trialsLayout.AutoSize = true;
        trialsLayout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        trialsLayout.ColumnCount = 3;
        trialsLayout.ColumnStyles.Add(new ColumnStyle());
        trialsLayout.ColumnStyles.Add(new ColumnStyle());
        trialsLayout.ColumnStyles.Add(new ColumnStyle());
        trialsLayout.Controls.Add(responseModeLabel, 0, 0);
        trialsLayout.Controls.Add(responseModeComboBox, 1, 0);
        trialsLayout.Controls.Add(responseModeHintLabel, 0, 1);
        trialsLayout.Controls.Add(measuredTrialsLabel, 0, 2);
        trialsLayout.Controls.Add(measuredTrialsNumeric, 1, 2);
        trialsLayout.Controls.Add(practiceTrialsLabel, 0, 3);
        trialsLayout.Controls.Add(practiceTrialsNumeric, 1, 3);
        trialsLayout.Controls.Add(trialCountNoteLabel, 0, 4);
        trialsLayout.Controls.Add(reactionTimeoutLabel, 0, 5);
        trialsLayout.Controls.Add(reactionTimeoutNumeric, 1, 5);
        trialsLayout.Controls.Add(reactionTimeoutUnitLabel, 2, 5);
        trialsLayout.Controls.Add(reactionTimeoutHintLabel, 0, 6);
        trialsLayout.Controls.Add(foreperiodHeaderLabel, 0, 7);
        trialsLayout.Controls.Add(foreperiodDistributionLabel, 0, 8);
        trialsLayout.Controls.Add(foreperiodDistributionComboBox, 1, 8);
        trialsLayout.Controls.Add(foreperiodMinLabel, 0, 9);
        trialsLayout.Controls.Add(foreperiodMinNumeric, 1, 9);
        trialsLayout.Controls.Add(foreperiodMinUnitLabel, 2, 9);
        trialsLayout.Controls.Add(foreperiodMaxLabel, 0, 10);
        trialsLayout.Controls.Add(foreperiodMaxNumeric, 1, 10);
        trialsLayout.Controls.Add(foreperiodMaxUnitLabel, 2, 10);
        trialsLayout.Controls.Add(foreperiodMeanLabel, 0, 11);
        trialsLayout.Controls.Add(foreperiodMeanNumeric, 1, 11);
        trialsLayout.Controls.Add(foreperiodMeanUnitLabel, 2, 11);
        trialsLayout.Controls.Add(sceneSettleDelayLabel, 0, 12);
        trialsLayout.Controls.Add(sceneSettleDelayNumeric, 1, 12);
        trialsLayout.Controls.Add(sceneSettleDelayUnitLabel, 2, 12);
        trialsLayout.Controls.Add(sceneSettleDelayHintLabel, 0, 13);
        trialsLayout.Location = new Point(0, 0);
        trialsLayout.Margin = new Padding(0);
        trialsLayout.Name = "trialsLayout";
        trialsLayout.RowCount = 14;
        trialsLayout.RowStyles.Add(new RowStyle());
        trialsLayout.RowStyles.Add(new RowStyle());
        trialsLayout.RowStyles.Add(new RowStyle());
        trialsLayout.RowStyles.Add(new RowStyle());
        trialsLayout.RowStyles.Add(new RowStyle());
        trialsLayout.RowStyles.Add(new RowStyle());
        trialsLayout.RowStyles.Add(new RowStyle());
        trialsLayout.RowStyles.Add(new RowStyle());
        trialsLayout.RowStyles.Add(new RowStyle());
        trialsLayout.RowStyles.Add(new RowStyle());
        trialsLayout.RowStyles.Add(new RowStyle());
        trialsLayout.RowStyles.Add(new RowStyle());
        trialsLayout.RowStyles.Add(new RowStyle());
        trialsLayout.RowStyles.Add(new RowStyle());
        trialsLayout.Size = new Size(470, 380);
        trialsLayout.TabIndex = 0;
        //
        // responseModeLabel
        //
        responseModeLabel.Anchor = AnchorStyles.Left;
        responseModeLabel.AutoSize = true;
        responseModeLabel.Margin = new Padding(0, 4, 10, 4);
        responseModeLabel.Name = "responseModeLabel";
        responseModeLabel.TabIndex = 0;
        responseModeLabel.Text = "Válasz módja";
        //
        // responseModeComboBox
        //
        responseModeComboBox.Anchor = AnchorStyles.Left;
        trialsLayout.SetColumnSpan(responseModeComboBox, 2);
        responseModeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        responseModeComboBox.Margin = new Padding(0, 3, 0, 3);
        responseModeComboBox.Name = "responseModeComboBox";
        responseModeComboBox.SelectedIndexChanged += control_Edited;
        responseModeComboBox.Size = new Size(232, 25);
        responseModeComboBox.TabIndex = 1;
        //
        // responseModeHintLabel
        //
        responseModeHintLabel.AutoSize = true;
        trialsLayout.SetColumnSpan(responseModeHintLabel, 3);
        responseModeHintLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 238);
        responseModeHintLabel.ForeColor = Color.FromArgb(96, 106, 120);
        responseModeHintLabel.Margin = new Padding(0, 0, 0, 6);
        responseModeHintLabel.MaximumSize = new Size(470, 0);
        responseModeHintLabel.Name = "responseModeHintLabel";
        responseModeHintLabel.TabIndex = 2;
        //
        // measuredTrialsLabel
        //
        measuredTrialsLabel.Anchor = AnchorStyles.Left;
        measuredTrialsLabel.AutoSize = true;
        measuredTrialsLabel.Margin = new Padding(0, 4, 10, 4);
        measuredTrialsLabel.Name = "measuredTrialsLabel";
        measuredTrialsLabel.TabIndex = 3;
        measuredTrialsLabel.Text = "Mért ingerek száma (mérésszám)";
        //
        // measuredTrialsNumeric
        //
        measuredTrialsNumeric.Anchor = AnchorStyles.Left;
        measuredTrialsNumeric.Margin = new Padding(0, 3, 6, 3);
        measuredTrialsNumeric.Name = "measuredTrialsNumeric";
        measuredTrialsNumeric.Size = new Size(96, 25);
        measuredTrialsNumeric.TabIndex = 4;
        measuredTrialsNumeric.TextAlign = HorizontalAlignment.Right;
        //
        // practiceTrialsLabel
        //
        practiceTrialsLabel.Anchor = AnchorStyles.Left;
        practiceTrialsLabel.AutoSize = true;
        practiceTrialsLabel.Margin = new Padding(0, 4, 10, 4);
        practiceTrialsLabel.Name = "practiceTrialsLabel";
        practiceTrialsLabel.TabIndex = 5;
        practiceTrialsLabel.Text = "Gyakorló (teszt) ingerek száma";
        //
        // practiceTrialsNumeric
        //
        practiceTrialsNumeric.Anchor = AnchorStyles.Left;
        practiceTrialsNumeric.Margin = new Padding(0, 3, 6, 3);
        practiceTrialsNumeric.Name = "practiceTrialsNumeric";
        practiceTrialsNumeric.Size = new Size(96, 25);
        practiceTrialsNumeric.TabIndex = 6;
        practiceTrialsNumeric.TextAlign = HorizontalAlignment.Right;
        //
        // trialCountNoteLabel
        //
        trialCountNoteLabel.AutoSize = true;
        trialsLayout.SetColumnSpan(trialCountNoteLabel, 3);
        trialCountNoteLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 238);
        trialCountNoteLabel.ForeColor = Color.FromArgb(64, 70, 82);
        trialCountNoteLabel.Margin = new Padding(0, 2, 0, 6);
        trialCountNoteLabel.MaximumSize = new Size(470, 0);
        trialCountNoteLabel.Name = "trialCountNoteLabel";
        trialCountNoteLabel.TabIndex = 7;
        //
        // reactionTimeoutLabel
        //
        reactionTimeoutLabel.Anchor = AnchorStyles.Left;
        reactionTimeoutLabel.AutoSize = true;
        reactionTimeoutLabel.Margin = new Padding(0, 4, 10, 4);
        reactionTimeoutLabel.Name = "reactionTimeoutLabel";
        reactionTimeoutLabel.TabIndex = 8;
        reactionTimeoutLabel.Text = "Maximális várakozás a válaszra";
        //
        // reactionTimeoutNumeric
        //
        reactionTimeoutNumeric.Anchor = AnchorStyles.Left;
        reactionTimeoutNumeric.Margin = new Padding(0, 3, 6, 3);
        reactionTimeoutNumeric.Name = "reactionTimeoutNumeric";
        reactionTimeoutNumeric.Size = new Size(96, 25);
        reactionTimeoutNumeric.TabIndex = 9;
        reactionTimeoutNumeric.TextAlign = HorizontalAlignment.Right;
        //
        // reactionTimeoutUnitLabel
        //
        reactionTimeoutUnitLabel.Anchor = AnchorStyles.Left;
        reactionTimeoutUnitLabel.AutoSize = true;
        reactionTimeoutUnitLabel.Margin = new Padding(0, 4, 0, 4);
        reactionTimeoutUnitLabel.Name = "reactionTimeoutUnitLabel";
        reactionTimeoutUnitLabel.TabIndex = 10;
        reactionTimeoutUnitLabel.Text = "ms";
        //
        // reactionTimeoutHintLabel
        //
        reactionTimeoutHintLabel.AutoSize = true;
        trialsLayout.SetColumnSpan(reactionTimeoutHintLabel, 3);
        reactionTimeoutHintLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 238);
        reactionTimeoutHintLabel.ForeColor = Color.FromArgb(96, 106, 120);
        reactionTimeoutHintLabel.Margin = new Padding(0, 0, 0, 6);
        reactionTimeoutHintLabel.MaximumSize = new Size(470, 0);
        reactionTimeoutHintLabel.Name = "reactionTimeoutHintLabel";
        reactionTimeoutHintLabel.TabIndex = 11;
        reactionTimeoutHintLabel.Text = "Ennyi idő után az inger eltűnik, a próba érvénytelen (időtúllépés), és jön a következő.";
        //
        // foreperiodHeaderLabel
        //
        foreperiodHeaderLabel.AutoSize = true;
        trialsLayout.SetColumnSpan(foreperiodHeaderLabel, 3);
        foreperiodHeaderLabel.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Regular, GraphicsUnit.Point, 238);
        foreperiodHeaderLabel.ForeColor = Color.FromArgb(52, 58, 70);
        foreperiodHeaderLabel.Margin = new Padding(0, 6, 0, 2);
        foreperiodHeaderLabel.Name = "foreperiodHeaderLabel";
        foreperiodHeaderLabel.TabIndex = 12;
        foreperiodHeaderLabel.Text = "Előperiódus (várakozás az inger előtt)";
        //
        // foreperiodDistributionLabel
        //
        foreperiodDistributionLabel.Anchor = AnchorStyles.Left;
        foreperiodDistributionLabel.AutoSize = true;
        foreperiodDistributionLabel.Margin = new Padding(18, 4, 10, 4);
        foreperiodDistributionLabel.Name = "foreperiodDistributionLabel";
        foreperiodDistributionLabel.TabIndex = 13;
        foreperiodDistributionLabel.Text = "eloszlás";
        //
        // foreperiodDistributionComboBox
        //
        foreperiodDistributionComboBox.Anchor = AnchorStyles.Left;
        trialsLayout.SetColumnSpan(foreperiodDistributionComboBox, 2);
        foreperiodDistributionComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        foreperiodDistributionComboBox.Margin = new Padding(0, 3, 0, 3);
        foreperiodDistributionComboBox.Name = "foreperiodDistributionComboBox";
        foreperiodDistributionComboBox.SelectedIndexChanged += control_Edited;
        foreperiodDistributionComboBox.Size = new Size(232, 25);
        foreperiodDistributionComboBox.TabIndex = 14;
        //
        // foreperiodMinLabel
        //
        foreperiodMinLabel.Anchor = AnchorStyles.Left;
        foreperiodMinLabel.AutoSize = true;
        foreperiodMinLabel.Margin = new Padding(18, 4, 10, 4);
        foreperiodMinLabel.Name = "foreperiodMinLabel";
        foreperiodMinLabel.TabIndex = 15;
        foreperiodMinLabel.Text = "legrövidebb";
        //
        // foreperiodMinNumeric
        //
        foreperiodMinNumeric.Anchor = AnchorStyles.Left;
        foreperiodMinNumeric.Margin = new Padding(0, 3, 6, 3);
        foreperiodMinNumeric.Name = "foreperiodMinNumeric";
        foreperiodMinNumeric.Size = new Size(96, 25);
        foreperiodMinNumeric.TabIndex = 16;
        foreperiodMinNumeric.TextAlign = HorizontalAlignment.Right;
        //
        // foreperiodMinUnitLabel
        //
        foreperiodMinUnitLabel.Anchor = AnchorStyles.Left;
        foreperiodMinUnitLabel.AutoSize = true;
        foreperiodMinUnitLabel.Margin = new Padding(0, 4, 0, 4);
        foreperiodMinUnitLabel.Name = "foreperiodMinUnitLabel";
        foreperiodMinUnitLabel.TabIndex = 17;
        foreperiodMinUnitLabel.Text = "ms";
        //
        // foreperiodMaxLabel
        //
        foreperiodMaxLabel.Anchor = AnchorStyles.Left;
        foreperiodMaxLabel.AutoSize = true;
        foreperiodMaxLabel.Margin = new Padding(18, 4, 10, 4);
        foreperiodMaxLabel.Name = "foreperiodMaxLabel";
        foreperiodMaxLabel.TabIndex = 18;
        foreperiodMaxLabel.Text = "leghosszabb";
        //
        // foreperiodMaxNumeric
        //
        foreperiodMaxNumeric.Anchor = AnchorStyles.Left;
        foreperiodMaxNumeric.Margin = new Padding(0, 3, 6, 3);
        foreperiodMaxNumeric.Name = "foreperiodMaxNumeric";
        foreperiodMaxNumeric.Size = new Size(96, 25);
        foreperiodMaxNumeric.TabIndex = 19;
        foreperiodMaxNumeric.TextAlign = HorizontalAlignment.Right;
        //
        // foreperiodMaxUnitLabel
        //
        foreperiodMaxUnitLabel.Anchor = AnchorStyles.Left;
        foreperiodMaxUnitLabel.AutoSize = true;
        foreperiodMaxUnitLabel.Margin = new Padding(0, 4, 0, 4);
        foreperiodMaxUnitLabel.Name = "foreperiodMaxUnitLabel";
        foreperiodMaxUnitLabel.TabIndex = 20;
        foreperiodMaxUnitLabel.Text = "ms";
        //
        // foreperiodMeanLabel
        //
        foreperiodMeanLabel.Anchor = AnchorStyles.Left;
        foreperiodMeanLabel.AutoSize = true;
        foreperiodMeanLabel.Margin = new Padding(18, 4, 10, 4);
        foreperiodMeanLabel.Name = "foreperiodMeanLabel";
        foreperiodMeanLabel.TabIndex = 21;
        foreperiodMeanLabel.Text = "exponenciális várható érték";
        //
        // foreperiodMeanNumeric
        //
        foreperiodMeanNumeric.Anchor = AnchorStyles.Left;
        foreperiodMeanNumeric.Margin = new Padding(0, 3, 6, 3);
        foreperiodMeanNumeric.Name = "foreperiodMeanNumeric";
        foreperiodMeanNumeric.Size = new Size(96, 25);
        foreperiodMeanNumeric.TabIndex = 22;
        foreperiodMeanNumeric.TextAlign = HorizontalAlignment.Right;
        //
        // foreperiodMeanUnitLabel
        //
        foreperiodMeanUnitLabel.Anchor = AnchorStyles.Left;
        foreperiodMeanUnitLabel.AutoSize = true;
        foreperiodMeanUnitLabel.Margin = new Padding(0, 4, 0, 4);
        foreperiodMeanUnitLabel.Name = "foreperiodMeanUnitLabel";
        foreperiodMeanUnitLabel.TabIndex = 23;
        foreperiodMeanUnitLabel.Text = "ms";
        //
        // sceneSettleDelayLabel
        //
        sceneSettleDelayLabel.Anchor = AnchorStyles.Left;
        sceneSettleDelayLabel.AutoSize = true;
        sceneSettleDelayLabel.Margin = new Padding(0, 10, 10, 4);
        sceneSettleDelayLabel.MaximumSize = new Size(220, 0);
        sceneSettleDelayLabel.Name = "sceneSettleDelayLabel";
        sceneSettleDelayLabel.TabIndex = 24;
        sceneSettleDelayLabel.Text = "Várakozás a mérési képernyő megjelenése után";
        //
        // sceneSettleDelayNumeric
        //
        sceneSettleDelayNumeric.Anchor = AnchorStyles.Left;
        sceneSettleDelayNumeric.Margin = new Padding(0, 9, 6, 3);
        sceneSettleDelayNumeric.Name = "sceneSettleDelayNumeric";
        sceneSettleDelayNumeric.Size = new Size(96, 25);
        sceneSettleDelayNumeric.TabIndex = 25;
        sceneSettleDelayNumeric.TextAlign = HorizontalAlignment.Right;
        //
        // sceneSettleDelayUnitLabel
        //
        sceneSettleDelayUnitLabel.Anchor = AnchorStyles.Left;
        sceneSettleDelayUnitLabel.AutoSize = true;
        sceneSettleDelayUnitLabel.Margin = new Padding(0, 10, 0, 4);
        sceneSettleDelayUnitLabel.Name = "sceneSettleDelayUnitLabel";
        sceneSettleDelayUnitLabel.TabIndex = 26;
        sceneSettleDelayUnitLabel.Text = "ms";
        //
        // sceneSettleDelayHintLabel
        //
        sceneSettleDelayHintLabel.AutoSize = true;
        trialsLayout.SetColumnSpan(sceneSettleDelayHintLabel, 3);
        sceneSettleDelayHintLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 238);
        sceneSettleDelayHintLabel.ForeColor = Color.FromArgb(96, 106, 120);
        sceneSettleDelayHintLabel.Margin = new Padding(0, 0, 0, 4);
        sceneSettleDelayHintLabel.MaximumSize = new Size(470, 0);
        sceneSettleDelayHintLabel.Name = "sceneSettleDelayHintLabel";
        sceneSettleDelayHintLabel.TabIndex = 27;
        sceneSettleDelayHintLabel.Text = "A mérési képernyő (beállított háttér, kereszt) megjelenése és az első inger között: a szem alkalmazkodik az új háttérhez, a szemkövető beáll.";
        //
        // TrialsSettingsTab
        //
        AutoScaleMode = AutoScaleMode.Inherit;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Color.FromArgb(253, 250, 250);
        Controls.Add(trialsLayout);
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point, 238);
        Margin = new Padding(0);
        Name = "TrialsSettingsTab";
        Size = new Size(470, 380);
        trialsLayout.ResumeLayout(false);
        trialsLayout.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)measuredTrialsNumeric).EndInit();
        ((System.ComponentModel.ISupportInitialize)practiceTrialsNumeric).EndInit();
        ((System.ComponentModel.ISupportInitialize)reactionTimeoutNumeric).EndInit();
        ((System.ComponentModel.ISupportInitialize)foreperiodMinNumeric).EndInit();
        ((System.ComponentModel.ISupportInitialize)foreperiodMaxNumeric).EndInit();
        ((System.ComponentModel.ISupportInitialize)foreperiodMeanNumeric).EndInit();
        ((System.ComponentModel.ISupportInitialize)sceneSettleDelayNumeric).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion
}
