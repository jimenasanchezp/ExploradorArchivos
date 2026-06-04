namespace ExploradorArchivos
{
    partial class Form1
    {
        /// <summary>
        /// Variable del diseñador necesaria.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Limpiar los recursos que se estén usando.
        /// </summary>
        /// <param name="disposing">true si los recursos administrados se deben desechar; false en caso contrario.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Código generado por el Diseñador de Windows Forms

        /// <summary>
        /// Método necesario para admitir el Diseñador. No se puede modificar
        /// el contenido de este método con el editor de código.
        /// </summary>
        private void InitializeComponent()
        {
            pnlTop = new Panel();
            btnExportarCSV = new Button();
            btnActualizar = new Button();
            btnNuevaCarpeta = new Button();
            pnlAddressBorder = new Panel();
            txtDireccion = new TextBox();
            btnSubir = new Button();
            btnAtras = new Button();
            btnCamara = new Button();
            btnCapturaPantalla = new Button();
            pnlBottom = new Panel();
            pnlTrash = new Panel();
            lblTrash = new Label();
            lblStatus = new Label();
            splitContainerMain = new SplitContainer();
            listViewPrincipal = new ListView();
            colNombre = new ColumnHeader();
            colTipo = new ColumnHeader();
            colTamano = new ColumnHeader();
            colInfo = new ColumnHeader();
            treeViewLateral = new TreeView();
            pnlSearch = new Panel();
            btnBuscar = new Button();
            pnlSearchBorder = new Panel();
            txtBuscar = new TextBox();
            pnlTop.SuspendLayout();
            pnlAddressBorder.SuspendLayout();
            pnlBottom.SuspendLayout();
            pnlTrash.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainerMain).BeginInit();
            splitContainerMain.Panel1.SuspendLayout();
            splitContainerMain.Panel2.SuspendLayout();
            splitContainerMain.SuspendLayout();
            pnlSearch.SuspendLayout();
            pnlSearchBorder.SuspendLayout();
            SuspendLayout();
            // 
            // pnlTop
            // 
            pnlTop.BackColor = Color.FromArgb(252, 228, 236);
            pnlTop.Controls.Add(btnExportarCSV);
            pnlTop.Controls.Add(btnActualizar);
            pnlTop.Controls.Add(btnNuevaCarpeta);
            pnlTop.Controls.Add(pnlAddressBorder);
            pnlTop.Controls.Add(btnSubir);
            pnlTop.Controls.Add(btnAtras);
            pnlTop.Dock = DockStyle.Top;
            pnlTop.Location = new Point(0, 0);
            pnlTop.Name = "pnlTop";
            pnlTop.Size = new Size(1200, 50);
            pnlTop.TabIndex = 0;
            // 
            // btnExportarCSV
            // 
            btnExportarCSV.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnExportarCSV.BackColor = Color.FromArgb(244, 143, 177);
            btnExportarCSV.FlatAppearance.BorderSize = 0;
            btnExportarCSV.FlatStyle = FlatStyle.Flat;
            btnExportarCSV.ForeColor = Color.White;
            btnExportarCSV.Location = new Point(1058, 10);
            btnExportarCSV.Name = "btnExportarCSV";
            btnExportarCSV.Size = new Size(130, 30);
            btnExportarCSV.TabIndex = 5;
            btnExportarCSV.Text = "📊 Exportar CSV";
            btnExportarCSV.UseVisualStyleBackColor = false;
            // 
            // btnActualizar
            // 
            btnActualizar.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnActualizar.FlatAppearance.BorderSize = 0;
            btnActualizar.FlatStyle = FlatStyle.Flat;
            btnActualizar.ForeColor = Color.FromArgb(45, 45, 45);
            btnActualizar.Location = new Point(1012, 10);
            btnActualizar.Name = "btnActualizar";
            btnActualizar.Size = new Size(40, 30);
            btnActualizar.TabIndex = 4;
            btnActualizar.Text = "⟳";
            btnActualizar.UseVisualStyleBackColor = true;
            // 
            // btnNuevaCarpeta
            // 
            btnNuevaCarpeta.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnNuevaCarpeta.BackColor = Color.FromArgb(244, 143, 177);
            btnNuevaCarpeta.FlatAppearance.BorderSize = 0;
            btnNuevaCarpeta.FlatStyle = FlatStyle.Flat;
            btnNuevaCarpeta.ForeColor = Color.White;
            btnNuevaCarpeta.Location = new Point(876, 10);
            btnNuevaCarpeta.Name = "btnNuevaCarpeta";
            btnNuevaCarpeta.Size = new Size(130, 30);
            btnNuevaCarpeta.TabIndex = 3;
            btnNuevaCarpeta.Text = "📁 Nueva Carpeta";
            btnNuevaCarpeta.UseVisualStyleBackColor = false;
            // 
            // pnlAddressBorder
            // 
            pnlAddressBorder.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnlAddressBorder.BackColor = Color.FromArgb(248, 187, 208);
            pnlAddressBorder.Controls.Add(txtDireccion);
            pnlAddressBorder.Location = new Point(104, 10);
            pnlAddressBorder.Name = "pnlAddressBorder";
            pnlAddressBorder.Padding = new Padding(2);
            pnlAddressBorder.Size = new Size(766, 30);
            pnlAddressBorder.TabIndex = 2;
            // 
            // txtDireccion
            // 
            txtDireccion.BackColor = Color.FromArgb(255, 245, 248);
            txtDireccion.BorderStyle = BorderStyle.None;
            txtDireccion.Dock = DockStyle.Fill;
            txtDireccion.Font = new Font("Segoe UI", 11F);
            txtDireccion.ForeColor = Color.FromArgb(45, 45, 45);
            txtDireccion.Location = new Point(2, 2);
            txtDireccion.Name = "txtDireccion";
            txtDireccion.Size = new Size(762, 25);
            txtDireccion.TabIndex = 0;
            // 
            // btnSubir
            // 
            btnSubir.FlatAppearance.BorderSize = 0;
            btnSubir.FlatStyle = FlatStyle.Flat;
            btnSubir.ForeColor = Color.FromArgb(45, 45, 45);
            btnSubir.Location = new Point(58, 10);
            btnSubir.Name = "btnSubir";
            btnSubir.Size = new Size(40, 30);
            btnSubir.TabIndex = 1;
            btnSubir.Text = "▲";
            btnSubir.UseVisualStyleBackColor = true;
            // 
            // btnAtras
            // 
            btnAtras.FlatAppearance.BorderSize = 0;
            btnAtras.FlatStyle = FlatStyle.Flat;
            btnAtras.ForeColor = Color.FromArgb(45, 45, 45);
            btnAtras.Location = new Point(12, 10);
            btnAtras.Name = "btnAtras";
            btnAtras.Size = new Size(40, 30);
            btnAtras.TabIndex = 0;
            btnAtras.Text = "◄";
            btnAtras.UseVisualStyleBackColor = true;
            // 
            // btnCamara
            // 
            btnCamara.Location = new Point(0, 0);
            btnCamara.Name = "btnCamara";
            btnCamara.Size = new Size(75, 23);
            btnCamara.TabIndex = 0;
            // 
            // btnCapturaPantalla
            // 
            btnCapturaPantalla.Location = new Point(0, 0);
            btnCapturaPantalla.Name = "btnCapturaPantalla";
            btnCapturaPantalla.Size = new Size(75, 23);
            btnCapturaPantalla.TabIndex = 0;
            // 
            // pnlBottom
            // 
            pnlBottom.BackColor = Color.FromArgb(252, 228, 236);
            pnlBottom.Controls.Add(pnlTrash);
            pnlBottom.Controls.Add(lblStatus);
            pnlBottom.Dock = DockStyle.Bottom;
            pnlBottom.Location = new Point(0, 660);
            pnlBottom.Name = "pnlBottom";
            pnlBottom.Size = new Size(1200, 40);
            pnlBottom.TabIndex = 1;
            // 
            // pnlTrash
            // 
            pnlTrash.AllowDrop = true;
            pnlTrash.BackColor = Color.FromArgb(255, 245, 248);
            pnlTrash.Controls.Add(lblTrash);
            pnlTrash.Dock = DockStyle.Right;
            pnlTrash.Location = new Point(950, 0);
            pnlTrash.Name = "pnlTrash";
            pnlTrash.Size = new Size(250, 40);
            pnlTrash.TabIndex = 1;
            // 
            // lblTrash
            // 
            lblTrash.Dock = DockStyle.Fill;
            lblTrash.ForeColor = Color.FromArgb(136, 136, 136);
            lblTrash.Location = new Point(0, 0);
            lblTrash.Name = "lblTrash";
            lblTrash.Size = new Size(250, 40);
            lblTrash.TabIndex = 0;
            lblTrash.Text = "🗑️ Arrastrar para eliminar";
            lblTrash.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblStatus
            // 
            lblStatus.AutoSize = true;
            lblStatus.ForeColor = Color.FromArgb(136, 136, 136);
            lblStatus.Location = new Point(12, 11);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(549, 23);
            lblStatus.TabIndex = 0;
            lblStatus.Text = "📁 0 carpetas  ·  📄 0 archivos  ·  🖼️ 0  ·  🎵 0  ·  🎬 0  ·  📝 0  ·  📦 0";
            // 
            // splitContainerMain
            // 
            splitContainerMain.Dock = DockStyle.Fill;
            splitContainerMain.FixedPanel = FixedPanel.Panel2;
            splitContainerMain.Location = new Point(0, 50);
            splitContainerMain.Name = "splitContainerMain";
            // 
            // splitContainerMain.Panel1
            // 
            splitContainerMain.Panel1.Controls.Add(listViewPrincipal);
            // 
            // splitContainerMain.Panel2
            // 
            splitContainerMain.Panel2.Controls.Add(treeViewLateral);
            splitContainerMain.Panel2.Controls.Add(pnlSearch);
            splitContainerMain.Size = new Size(1200, 610);
            splitContainerMain.SplitterDistance = 904;
            splitContainerMain.SplitterWidth = 2;
            splitContainerMain.TabIndex = 2;
            // 
            // listViewPrincipal
            // 
            listViewPrincipal.AllowDrop = true;
            listViewPrincipal.BackColor = Color.FromArgb(255, 245, 248);
            listViewPrincipal.BorderStyle = BorderStyle.None;
            listViewPrincipal.Columns.AddRange(new ColumnHeader[] { colNombre, colTipo, colTamano, colInfo });
            listViewPrincipal.Dock = DockStyle.Fill;
            listViewPrincipal.ForeColor = Color.FromArgb(45, 45, 45);
            listViewPrincipal.FullRowSelect = true;
            listViewPrincipal.Location = new Point(0, 0);
            listViewPrincipal.Name = "listViewPrincipal";
            listViewPrincipal.OwnerDraw = true;
            listViewPrincipal.Size = new Size(904, 610);
            listViewPrincipal.TabIndex = 0;
            listViewPrincipal.UseCompatibleStateImageBehavior = false;
            listViewPrincipal.View = View.Details;
            // 
            // colNombre
            // 
            colNombre.Text = "Nombre";
            colNombre.Width = 350;
            // 
            // colTipo
            // 
            colTipo.Text = "Tipo";
            colTipo.Width = 120;
            // 
            // colTamano
            // 
            colTamano.Text = "Tamaño";
            colTamano.Width = 120;
            // 
            // colInfo
            // 
            colInfo.Text = "Contenido / Info";
            colInfo.Width = 240;
            // 
            // treeViewLateral
            // 
            treeViewLateral.BackColor = Color.FromArgb(252, 228, 236);
            treeViewLateral.BorderStyle = BorderStyle.None;
            treeViewLateral.Dock = DockStyle.Fill;
            treeViewLateral.DrawMode = TreeViewDrawMode.OwnerDrawAll;
            treeViewLateral.ForeColor = Color.FromArgb(45, 45, 45);
            treeViewLateral.FullRowSelect = true;
            treeViewLateral.ItemHeight = 28;
            treeViewLateral.Location = new Point(0, 50);
            treeViewLateral.Name = "treeViewLateral";
            treeViewLateral.ShowLines = false;
            treeViewLateral.ShowPlusMinus = false;
            treeViewLateral.Size = new Size(294, 560);
            treeViewLateral.TabIndex = 1;
            // 
            // pnlSearch
            // 
            pnlSearch.BackColor = Color.FromArgb(252, 228, 236);
            pnlSearch.Controls.Add(btnBuscar);
            pnlSearch.Controls.Add(pnlSearchBorder);
            pnlSearch.Dock = DockStyle.Top;
            pnlSearch.Location = new Point(0, 0);
            pnlSearch.Name = "pnlSearch";
            pnlSearch.Size = new Size(294, 50);
            pnlSearch.TabIndex = 0;
            // 
            // btnBuscar
            // 
            btnBuscar.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnBuscar.BackColor = Color.FromArgb(244, 143, 177);
            btnBuscar.FlatAppearance.BorderSize = 0;
            btnBuscar.FlatStyle = FlatStyle.Flat;
            btnBuscar.ForeColor = Color.White;
            btnBuscar.Location = new Point(211, 10);
            btnBuscar.Name = "btnBuscar";
            btnBuscar.Size = new Size(75, 30);
            btnBuscar.TabIndex = 1;
            btnBuscar.Text = "Buscar";
            btnBuscar.UseVisualStyleBackColor = false;
            // 
            // pnlSearchBorder
            // 
            pnlSearchBorder.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnlSearchBorder.BackColor = Color.FromArgb(248, 187, 208);
            pnlSearchBorder.Controls.Add(txtBuscar);
            pnlSearchBorder.Location = new Point(6, 10);
            pnlSearchBorder.Name = "pnlSearchBorder";
            pnlSearchBorder.Padding = new Padding(2);
            pnlSearchBorder.Size = new Size(199, 30);
            pnlSearchBorder.TabIndex = 0;
            // 
            // txtBuscar
            // 
            txtBuscar.BackColor = Color.FromArgb(255, 245, 248);
            txtBuscar.BorderStyle = BorderStyle.None;
            txtBuscar.Dock = DockStyle.Fill;
            txtBuscar.Font = new Font("Segoe UI", 11F);
            txtBuscar.ForeColor = Color.FromArgb(45, 45, 45);
            txtBuscar.Location = new Point(2, 2);
            txtBuscar.Multiline = true;
            txtBuscar.Name = "txtBuscar";
            txtBuscar.Size = new Size(195, 26);
            txtBuscar.TabIndex = 0;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(9F, 23F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(255, 245, 248);
            ClientSize = new Size(1200, 700);
            Controls.Add(splitContainerMain);
            Controls.Add(pnlBottom);
            Controls.Add(pnlTop);
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            FormBorderStyle = FormBorderStyle.None;
            Margin = new Padding(4);
            MinimumSize = new Size(800, 500);
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Explorador de Archivos";
            pnlTop.ResumeLayout(false);
            pnlAddressBorder.ResumeLayout(false);
            pnlAddressBorder.PerformLayout();
            pnlBottom.ResumeLayout(false);
            pnlBottom.PerformLayout();
            pnlTrash.ResumeLayout(false);
            splitContainerMain.Panel1.ResumeLayout(false);
            splitContainerMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainerMain).EndInit();
            splitContainerMain.ResumeLayout(false);
            pnlSearch.ResumeLayout(false);
            pnlSearchBorder.ResumeLayout(false);
            pnlSearchBorder.PerformLayout();
            ResumeLayout(false);

        }

        #endregion

        // Declaración de Variables de los Controles
        public System.Windows.Forms.Panel pnlTop = null!;
        public System.Windows.Forms.Button btnAtras = null!;
        public System.Windows.Forms.Button btnSubir = null!;
        public System.Windows.Forms.Panel pnlAddressBorder = null!;
        public System.Windows.Forms.TextBox txtDireccion = null!;
        public System.Windows.Forms.Button btnNuevaCarpeta = null!;
        public System.Windows.Forms.Button btnActualizar = null!;
        public System.Windows.Forms.Button btnExportarCSV = null!;
        public System.Windows.Forms.Button btnCamara = null!;
        public System.Windows.Forms.Button btnCapturaPantalla = null!;

        public System.Windows.Forms.Panel pnlBottom = null!;
        public System.Windows.Forms.Label lblStatus = null!;
        public System.Windows.Forms.Panel pnlTrash = null!;
        public System.Windows.Forms.Label lblTrash = null!;

        public System.Windows.Forms.SplitContainer splitContainerMain = null!;
        public System.Windows.Forms.ListView listViewPrincipal = null!;
        private System.Windows.Forms.ColumnHeader colNombre = null!;
        private System.Windows.Forms.ColumnHeader colTipo = null!;
        private System.Windows.Forms.ColumnHeader colTamano = null!;
        private System.Windows.Forms.ColumnHeader colInfo = null!;

        public System.Windows.Forms.Panel pnlSearch = null!;
        public System.Windows.Forms.Panel pnlSearchBorder = null!;
        public System.Windows.Forms.TextBox txtBuscar = null!;
        public System.Windows.Forms.Button btnBuscar = null!;
        public System.Windows.Forms.TreeView treeViewLateral = null!;
    }
}