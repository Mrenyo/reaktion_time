namespace ReactionTimeMeasurementApp.UI;

partial class MeasurementSettingsDialog
{
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel rootLayout;
    private Panel bannerPanel;
    private TextBox bannerTextBox;
    private TabControl settingsTabControl;
    private TabPage trialsTabPage;
    private TabPage appearanceTabPage;
    private TabPage gazeTabPage;
    private TabPage hardwareTabPage;
    private SettingsPreviewPanel previewPanel;
    private Panel messagesPanel;
    private RichTextBox messagesTextBox;
    private TableLayoutPanel buttonsLayout;
    private Button defaultsButton;
    private Button cancelButton;
    private Button saveButton;
    private TrialsSettingsTab trialsTab;
    private AppearanceSettingsTab appearanceTab;
    private GazeSettingsTab gazeTab;
    private HardwareSettingsTab hardwareTab;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        rootLayout = new TableLayoutPanel();
        bannerPanel = new Panel();
        bannerTextBox = new TextBox();
        settingsTabControl = new TabControl();
        trialsTabPage = new TabPage();
        trialsTab = new TrialsSettingsTab();
        appearanceTabPage = new TabPage();
        appearanceTab = new AppearanceSettingsTab();
        gazeTabPage = new TabPage();
        gazeTab = new GazeSettingsTab();
        hardwareTabPage = new TabPage();
        hardwareTab = new HardwareSettingsTab();
        previewPanel = new SettingsPreviewPanel();
        messagesPanel = new Panel();
        messagesTextBox = new RichTextBox();
        buttonsLayout = new TableLayoutPanel();
        defaultsButton = new Button();
        cancelButton = new Button();
        saveButton = new Button();
        rootLayout.SuspendLayout();
        bannerPanel.SuspendLayout();
        settingsTabControl.SuspendLayout();
        trialsTabPage.SuspendLayout();
        appearanceTabPage.SuspendLayout();
        gazeTabPage.SuspendLayout();
        hardwareTabPage.SuspendLayout();
        messagesPanel.SuspendLayout();
        buttonsLayout.SuspendLayout();
        SuspendLayout();
        //
        // rootLayout
        //
        rootLayout.BackColor = Color.FromArgb(248, 244, 243);
        rootLayout.ColumnCount = 2;
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 352F));
        rootLayout.Controls.Add(bannerPanel, 0, 0);
        rootLayout.Controls.Add(settingsTabControl, 0, 1);
        rootLayout.Controls.Add(previewPanel, 1, 0);
        rootLayout.Controls.Add(messagesPanel, 0, 2);
        rootLayout.Controls.Add(buttonsLayout, 0, 3);
        rootLayout.Dock = DockStyle.Fill;
        rootLayout.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point, 238);
        rootLayout.Location = new Point(0, 0);
        rootLayout.Margin = new Padding(0);
        rootLayout.Name = "rootLayout";
        rootLayout.Padding = new Padding(14, 12, 14, 12);
        rootLayout.RowCount = 4;
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.Size = new Size(920, 596);
        rootLayout.TabIndex = 0;
        //
        // bannerPanel
        //
        bannerPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        bannerPanel.BackColor = Color.FromArgb(255, 243, 205);
        bannerPanel.Controls.Add(bannerTextBox);
        bannerPanel.Location = new Point(14, 12);
        bannerPanel.Margin = new Padding(0, 0, 12, 10);
        bannerPanel.Name = "bannerPanel";
        bannerPanel.Padding = new Padding(10, 8, 10, 8);
        bannerPanel.Size = new Size(528, 60);
        bannerPanel.TabIndex = 0;
        bannerPanel.Visible = false;
        //
        // bannerTextBox
        //
        bannerTextBox.AutoSize = false;
        bannerTextBox.BackColor = Color.FromArgb(255, 243, 205);
        bannerTextBox.BorderStyle = BorderStyle.None;
        bannerTextBox.Dock = DockStyle.Fill;
        bannerTextBox.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 238);
        bannerTextBox.ForeColor = Color.FromArgb(110, 70, 0);
        bannerTextBox.Location = new Point(10, 8);
        bannerTextBox.Multiline = true;
        bannerTextBox.Name = "bannerTextBox";
        bannerTextBox.ReadOnly = true;
        bannerTextBox.Size = new Size(508, 44);
        bannerTextBox.TabIndex = 0;
        bannerTextBox.TabStop = false;
        //
        // settingsTabControl
        //
        settingsTabControl.Controls.Add(trialsTabPage);
        settingsTabControl.Controls.Add(appearanceTabPage);
        settingsTabControl.Controls.Add(gazeTabPage);
        settingsTabControl.Controls.Add(hardwareTabPage);
        settingsTabControl.Dock = DockStyle.Fill;
        settingsTabControl.Location = new Point(14, 12);
        settingsTabControl.Margin = new Padding(0, 0, 12, 0);
        settingsTabControl.Name = "settingsTabControl";
        settingsTabControl.Padding = new Point(10, 4);
        settingsTabControl.SelectedIndex = 0;
        settingsTabControl.Size = new Size(528, 436);
        settingsTabControl.TabIndex = 1;
        //
        // trialsTabPage
        //
        trialsTabPage.AutoScroll = true;
        trialsTabPage.BackColor = Color.FromArgb(253, 250, 250);
        trialsTabPage.Controls.Add(trialsTab);
        trialsTabPage.Location = new Point(4, 30);
        trialsTabPage.Name = "trialsTabPage";
        trialsTabPage.Padding = new Padding(10, 8, 6, 6);
        trialsTabPage.Size = new Size(520, 402);
        trialsTabPage.TabIndex = 0;
        trialsTabPage.Text = "Ingerek és időzítés";
        //
        // trialsTab
        //
        trialsTab.Location = new Point(10, 8);
        trialsTab.Margin = new Padding(0);
        trialsTab.Name = "trialsTab";
        trialsTab.TabIndex = 0;
        //
        // appearanceTabPage
        //
        appearanceTabPage.AutoScroll = true;
        appearanceTabPage.BackColor = Color.FromArgb(253, 250, 250);
        appearanceTabPage.Controls.Add(appearanceTab);
        appearanceTabPage.Location = new Point(4, 30);
        appearanceTabPage.Name = "appearanceTabPage";
        appearanceTabPage.Padding = new Padding(10, 8, 6, 6);
        appearanceTabPage.Size = new Size(520, 402);
        appearanceTabPage.TabIndex = 1;
        appearanceTabPage.Text = "Megjelenés";
        //
        // appearanceTab
        //
        appearanceTab.Location = new Point(10, 8);
        appearanceTab.Margin = new Padding(0);
        appearanceTab.Name = "appearanceTab";
        appearanceTab.TabIndex = 0;
        //
        // gazeTabPage
        //
        gazeTabPage.AutoScroll = true;
        gazeTabPage.BackColor = Color.FromArgb(253, 250, 250);
        gazeTabPage.Controls.Add(gazeTab);
        gazeTabPage.Location = new Point(4, 30);
        gazeTabPage.Name = "gazeTabPage";
        gazeTabPage.Padding = new Padding(10, 8, 6, 6);
        gazeTabPage.Size = new Size(520, 402);
        gazeTabPage.TabIndex = 2;
        gazeTabPage.Text = "Szemmozgás";
        //
        // gazeTab
        //
        gazeTab.Location = new Point(10, 8);
        gazeTab.Margin = new Padding(0);
        gazeTab.Name = "gazeTab";
        gazeTab.TabIndex = 0;
        //
        // hardwareTabPage
        //
        hardwareTabPage.AutoScroll = true;
        hardwareTabPage.BackColor = Color.FromArgb(253, 250, 250);
        hardwareTabPage.Controls.Add(hardwareTab);
        hardwareTabPage.Location = new Point(4, 30);
        hardwareTabPage.Name = "hardwareTabPage";
        hardwareTabPage.Padding = new Padding(10, 8, 6, 6);
        hardwareTabPage.Size = new Size(520, 402);
        hardwareTabPage.TabIndex = 3;
        hardwareTabPage.Text = "Hardver";
        //
        // hardwareTab
        //
        hardwareTab.Location = new Point(10, 8);
        hardwareTab.Margin = new Padding(0);
        hardwareTab.Name = "hardwareTab";
        hardwareTab.TabIndex = 0;
        //
        // previewPanel
        //
        previewPanel.BackColor = Color.FromArgb(248, 244, 243);
        previewPanel.Dock = DockStyle.Fill;
        previewPanel.Location = new Point(554, 12);
        previewPanel.Margin = new Padding(0, 2, 0, 0);
        previewPanel.Name = "previewPanel";
        rootLayout.SetRowSpan(previewPanel, 2);
        previewPanel.Size = new Size(352, 436);
        previewPanel.TabIndex = 2;
        previewPanel.TabStop = false;
        //
        // messagesPanel
        //
        messagesPanel.BackColor = Color.FromArgb(253, 250, 250);
        rootLayout.SetColumnSpan(messagesPanel, 2);
        messagesPanel.Controls.Add(messagesTextBox);
        messagesPanel.Dock = DockStyle.Fill;
        messagesPanel.Location = new Point(14, 458);
        messagesPanel.Margin = new Padding(0, 10, 0, 8);
        messagesPanel.Name = "messagesPanel";
        messagesPanel.Padding = new Padding(10, 6, 4, 6);
        messagesPanel.Size = new Size(892, 74);
        messagesPanel.TabIndex = 3;
        //
        // messagesTextBox
        //
        messagesTextBox.BackColor = Color.FromArgb(253, 250, 250);
        messagesTextBox.BorderStyle = BorderStyle.None;
        messagesTextBox.DetectUrls = false;
        messagesTextBox.Dock = DockStyle.Fill;
        messagesTextBox.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 238);
        messagesTextBox.Location = new Point(10, 6);
        messagesTextBox.Name = "messagesTextBox";
        messagesTextBox.ReadOnly = true;
        messagesTextBox.ScrollBars = RichTextBoxScrollBars.Vertical;
        messagesTextBox.Size = new Size(878, 62);
        messagesTextBox.TabIndex = 0;
        messagesTextBox.TabStop = false;
        messagesTextBox.Text = "";
        //
        // buttonsLayout
        //
        buttonsLayout.AutoSize = true;
        buttonsLayout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        buttonsLayout.ColumnCount = 4;
        buttonsLayout.ColumnStyles.Add(new ColumnStyle());
        buttonsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        buttonsLayout.ColumnStyles.Add(new ColumnStyle());
        buttonsLayout.ColumnStyles.Add(new ColumnStyle());
        rootLayout.SetColumnSpan(buttonsLayout, 2);
        buttonsLayout.Controls.Add(defaultsButton, 0, 0);
        buttonsLayout.Controls.Add(cancelButton, 2, 0);
        buttonsLayout.Controls.Add(saveButton, 3, 0);
        buttonsLayout.Dock = DockStyle.Fill;
        buttonsLayout.Location = new Point(14, 540);
        buttonsLayout.Margin = new Padding(0);
        buttonsLayout.Name = "buttonsLayout";
        buttonsLayout.RowCount = 1;
        buttonsLayout.RowStyles.Add(new RowStyle());
        buttonsLayout.Size = new Size(892, 44);
        buttonsLayout.TabIndex = 4;
        //
        // defaultsButton
        //
        defaultsButton.BackColor = Color.FromArgb(244, 238, 237);
        defaultsButton.Cursor = Cursors.Hand;
        defaultsButton.FlatAppearance.BorderColor = Color.FromArgb(222, 212, 210);
        defaultsButton.FlatStyle = FlatStyle.Flat;
        defaultsButton.ForeColor = Color.FromArgb(85, 94, 114);
        defaultsButton.Margin = new Padding(0);
        defaultsButton.Name = "defaultsButton";
        defaultsButton.Size = new Size(130, 38);
        defaultsButton.TabIndex = 0;
        defaultsButton.Text = "Alapértékek";
        defaultsButton.UseVisualStyleBackColor = false;
        defaultsButton.Click += defaultsButton_Click;
        //
        // cancelButton
        //
        cancelButton.BackColor = Color.FromArgb(244, 238, 237);
        cancelButton.CausesValidation = false;
        cancelButton.Cursor = Cursors.Hand;
        cancelButton.DialogResult = DialogResult.Cancel;
        cancelButton.FlatAppearance.BorderColor = Color.FromArgb(222, 212, 210);
        cancelButton.FlatStyle = FlatStyle.Flat;
        cancelButton.ForeColor = Color.FromArgb(85, 94, 114);
        cancelButton.Margin = new Padding(0, 0, 10, 0);
        cancelButton.Name = "cancelButton";
        cancelButton.Size = new Size(120, 38);
        cancelButton.TabIndex = 1;
        cancelButton.Text = "Mégse";
        cancelButton.UseVisualStyleBackColor = false;
        //
        // saveButton
        //
        saveButton.BackColor = Color.FromArgb(255, 91, 24);
        saveButton.Cursor = Cursors.Hand;
        saveButton.FlatAppearance.BorderColor = Color.FromArgb(255, 91, 24);
        saveButton.FlatAppearance.BorderSize = 0;
        saveButton.FlatStyle = FlatStyle.Flat;
        saveButton.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold, GraphicsUnit.Point, 238);
        saveButton.ForeColor = Color.White;
        saveButton.Margin = new Padding(0);
        saveButton.Name = "saveButton";
        saveButton.Size = new Size(130, 38);
        saveButton.TabIndex = 2;
        saveButton.Text = "Mentés";
        saveButton.UseVisualStyleBackColor = false;
        saveButton.Click += saveButton_Click;
        //
        // MeasurementSettingsDialog
        //
        AcceptButton = saveButton;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(248, 244, 243);
        CancelButton = cancelButton;
        ClientSize = new Size(920, 596);
        Controls.Add(rootLayout);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "MeasurementSettingsDialog";
        ShowIcon = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Mérési beállítások";
        rootLayout.ResumeLayout(false);
        rootLayout.PerformLayout();
        bannerPanel.ResumeLayout(false);
        bannerPanel.PerformLayout();
        settingsTabControl.ResumeLayout(false);
        trialsTabPage.ResumeLayout(false);
        trialsTabPage.PerformLayout();
        appearanceTabPage.ResumeLayout(false);
        appearanceTabPage.PerformLayout();
        gazeTabPage.ResumeLayout(false);
        gazeTabPage.PerformLayout();
        hardwareTabPage.ResumeLayout(false);
        hardwareTabPage.PerformLayout();
        messagesPanel.ResumeLayout(false);
        buttonsLayout.ResumeLayout(false);
        ResumeLayout(false);
    }

    #endregion
}
