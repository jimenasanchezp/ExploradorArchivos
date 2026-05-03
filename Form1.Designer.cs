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
            this.pnlTop = new System.Windows.Forms.Panel();
            this.btnExportarCSV = new System.Windows.Forms.Button();
            this.btnActualizar = new System.Windows.Forms.Button();
            this.btnNuevaCarpeta = new System.Windows.Forms.Button();
            this.pnlAddressBorder = new System.Windows.Forms.Panel();
            this.txtDireccion = new System.Windows.Forms.TextBox();
            this.btnSubir = new System.Windows.Forms.Button();
            this.btnAtras = new System.Windows.Forms.Button();
            this.pnlBottom = new System.Windows.Forms.Panel();
            this.pnlTrash = new System.Windows.Forms.Panel();
            this.lblTrash = new System.Windows.Forms.Label();
            this.lblStatus = new System.Windows.Forms.Label();
            this.splitContainerMain = new System.Windows.Forms.SplitContainer();
            this.listViewPrincipal = new System.Windows.Forms.ListView();
            this.colNombre = new System.Windows.Forms.ColumnHeader();
            this.colTipo = new System.Windows.Forms.ColumnHeader();
            this.colTamano = new System.Windows.Forms.ColumnHeader();
            this.colInfo = new System.Windows.Forms.ColumnHeader();
            this.treeViewLateral = new System.Windows.Forms.TreeView();
            this.pnlSearch = new System.Windows.Forms.Panel();
            this.btnBuscar = new System.Windows.Forms.Button();
            this.pnlSearchBorder = new System.Windows.Forms.Panel();
            this.txtBuscar = new System.Windows.Forms.TextBox();
            this.pnlTop.SuspendLayout();
            this.pnlAddressBorder.SuspendLayout();
            this.pnlBottom.SuspendLayout();
            this.pnlTrash.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).BeginInit();
            this.splitContainerMain.Panel1.SuspendLayout();
            this.splitContainerMain.Panel2.SuspendLayout();
            this.splitContainerMain.SuspendLayout();
            this.pnlSearch.SuspendLayout();
            this.pnlSearchBorder.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlTop
            // 
            this.pnlTop.BackColor = System.Drawing.ColorTranslator.FromHtml("#FCE4EC");
            this.pnlTop.Controls.Add(this.btnExportarCSV);
            this.pnlTop.Controls.Add(this.btnActualizar);
            this.pnlTop.Controls.Add(this.btnNuevaCarpeta);
            this.pnlTop.Controls.Add(this.pnlAddressBorder);
            this.pnlTop.Controls.Add(this.btnSubir);
            this.pnlTop.Controls.Add(this.btnAtras);
            this.pnlTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlTop.Location = new System.Drawing.Point(0, 0);
            this.pnlTop.Name = "pnlTop";
            this.pnlTop.Size = new System.Drawing.Size(1200, 50);
            this.pnlTop.TabIndex = 0;
            // 
            // btnExportarCSV
            // 
            this.btnExportarCSV.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnExportarCSV.BackColor = System.Drawing.ColorTranslator.FromHtml("#F48FB1");
            this.btnExportarCSV.FlatAppearance.BorderSize = 0;
            this.btnExportarCSV.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnExportarCSV.ForeColor = System.Drawing.Color.White;
            this.btnExportarCSV.Location = new System.Drawing.Point(1058, 10);
            this.btnExportarCSV.Name = "btnExportarCSV";
            this.btnExportarCSV.Size = new System.Drawing.Size(130, 30);
            this.btnExportarCSV.TabIndex = 5;
            this.btnExportarCSV.Text = "📊 Exportar CSV";
            this.btnExportarCSV.UseVisualStyleBackColor = false;
            // 
            // btnActualizar
            // 
            this.btnActualizar.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnActualizar.FlatAppearance.BorderSize = 0;
            this.btnActualizar.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnActualizar.ForeColor = System.Drawing.ColorTranslator.FromHtml("#2D2D2D");
            this.btnActualizar.Location = new System.Drawing.Point(1012, 10);
            this.btnActualizar.Name = "btnActualizar";
            this.btnActualizar.Size = new System.Drawing.Size(40, 30);
            this.btnActualizar.TabIndex = 4;
            this.btnActualizar.Text = "⟳";
            this.btnActualizar.UseVisualStyleBackColor = true;
            // 
            // btnNuevaCarpeta
            // 
            this.btnNuevaCarpeta.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnNuevaCarpeta.BackColor = System.Drawing.ColorTranslator.FromHtml("#F48FB1");
            this.btnNuevaCarpeta.FlatAppearance.BorderSize = 0;
            this.btnNuevaCarpeta.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnNuevaCarpeta.ForeColor = System.Drawing.Color.White;
            this.btnNuevaCarpeta.Location = new System.Drawing.Point(876, 10);
            this.btnNuevaCarpeta.Name = "btnNuevaCarpeta";
            this.btnNuevaCarpeta.Size = new System.Drawing.Size(130, 30);
            this.btnNuevaCarpeta.TabIndex = 3;
            this.btnNuevaCarpeta.Text = "📁 Nueva Carpeta";
            this.btnNuevaCarpeta.UseVisualStyleBackColor = false;
            // 
            // pnlAddressBorder
            // 
            this.pnlAddressBorder.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pnlAddressBorder.BackColor = System.Drawing.ColorTranslator.FromHtml("#F8BBD0");
            this.pnlAddressBorder.Controls.Add(this.txtDireccion);
            this.pnlAddressBorder.Location = new System.Drawing.Point(104, 10);
            this.pnlAddressBorder.Name = "pnlAddressBorder";
            this.pnlAddressBorder.Padding = new System.Windows.Forms.Padding(2);
            this.pnlAddressBorder.Size = new System.Drawing.Size(766, 30);
            this.pnlAddressBorder.TabIndex = 2;
            // 
            // txtDireccion
            // 
            this.txtDireccion.BackColor = System.Drawing.ColorTranslator.FromHtml("#FFF5F8");
            this.txtDireccion.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.txtDireccion.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtDireccion.Font = new System.Drawing.Font("Segoe UI", 11F);
            this.txtDireccion.ForeColor = System.Drawing.ColorTranslator.FromHtml("#2D2D2D");
            this.txtDireccion.Location = new System.Drawing.Point(2, 2);
            this.txtDireccion.Multiline = true;
            this.txtDireccion.Name = "txtDireccion";
            this.txtDireccion.Size = new System.Drawing.Size(762, 26);
            this.txtDireccion.TabIndex = 0;
            // 
            // btnSubir
            // 
            this.btnSubir.FlatAppearance.BorderSize = 0;
            this.btnSubir.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSubir.ForeColor = System.Drawing.ColorTranslator.FromHtml("#2D2D2D");
            this.btnSubir.Location = new System.Drawing.Point(58, 10);
            this.btnSubir.Name = "btnSubir";
            this.btnSubir.Size = new System.Drawing.Size(40, 30);
            this.btnSubir.TabIndex = 1;
            this.btnSubir.Text = "▲";
            this.btnSubir.UseVisualStyleBackColor = true;
            // 
            // btnAtras
            // 
            this.btnAtras.FlatAppearance.BorderSize = 0;
            this.btnAtras.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnAtras.ForeColor = System.Drawing.ColorTranslator.FromHtml("#2D2D2D");
            this.btnAtras.Location = new System.Drawing.Point(12, 10);
            this.btnAtras.Name = "btnAtras";
            this.btnAtras.Size = new System.Drawing.Size(40, 30);
            this.btnAtras.TabIndex = 0;
            this.btnAtras.Text = "◄";
            this.btnAtras.UseVisualStyleBackColor = true;
            // 
            // pnlBottom
            // 
            this.pnlBottom.BackColor = System.Drawing.ColorTranslator.FromHtml("#FCE4EC");
            this.pnlBottom.Controls.Add(this.pnlTrash);
            this.pnlBottom.Controls.Add(this.lblStatus);
            this.pnlBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlBottom.Location = new System.Drawing.Point(0, 660);
            this.pnlBottom.Name = "pnlBottom";
            this.pnlBottom.Size = new System.Drawing.Size(1200, 40);
            this.pnlBottom.TabIndex = 1;
            // 
            // pnlTrash
            // 
            this.pnlTrash.AllowDrop = true;
            this.pnlTrash.BackColor = System.Drawing.ColorTranslator.FromHtml("#FFF5F8");
            this.pnlTrash.Controls.Add(this.lblTrash);
            this.pnlTrash.Dock = System.Windows.Forms.DockStyle.Right;
            this.pnlTrash.Location = new System.Drawing.Point(950, 0);
            this.pnlTrash.Name = "pnlTrash";
            this.pnlTrash.Size = new System.Drawing.Size(250, 40);
            this.pnlTrash.TabIndex = 1;
            // 
            // lblTrash
            // 
            this.lblTrash.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTrash.ForeColor = System.Drawing.ColorTranslator.FromHtml("#888888");
            this.lblTrash.Location = new System.Drawing.Point(0, 0);
            this.lblTrash.Name = "lblTrash";
            this.lblTrash.Size = new System.Drawing.Size(250, 40);
            this.lblTrash.TabIndex = 0;
            this.lblTrash.Text = "🗑️ Arrastrar para eliminar";
            this.lblTrash.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = true;
            this.lblStatus.ForeColor = System.Drawing.ColorTranslator.FromHtml("#888888");
            this.lblStatus.Location = new System.Drawing.Point(12, 11);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(434, 19);
            this.lblStatus.TabIndex = 0;
            this.lblStatus.Text = "📁 0 carpetas  ·  📄 0 archivos  ·  🖼️ 0  ·  🎵 0  ·  🎬 0  ·  📝 0  ·  📦 0";
            // 
            // splitContainerMain
            // 
            this.splitContainerMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerMain.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitContainerMain.Location = new System.Drawing.Point(0, 50);
            this.splitContainerMain.Name = "splitContainerMain";
            // 
            // splitContainerMain.Panel1
            // 
            this.splitContainerMain.Panel1.Controls.Add(this.listViewPrincipal);
            // 
            // splitContainerMain.Panel2
            // 
            this.splitContainerMain.Panel2.Controls.Add(this.treeViewLateral);
            this.splitContainerMain.Panel2.Controls.Add(this.pnlSearch);
            this.splitContainerMain.Size = new System.Drawing.Size(1200, 610);
            this.splitContainerMain.SplitterDistance = 850;
            this.splitContainerMain.SplitterWidth = 2;
            this.splitContainerMain.TabIndex = 2;
            // 
            // listViewPrincipal
            // 
            this.listViewPrincipal.AllowDrop = true;
            this.listViewPrincipal.BackColor = System.Drawing.ColorTranslator.FromHtml("#FFF5F8");
            this.listViewPrincipal.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.listViewPrincipal.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colNombre,
            this.colTipo,
            this.colTamano,
            this.colInfo});
            this.listViewPrincipal.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listViewPrincipal.ForeColor = System.Drawing.ColorTranslator.FromHtml("#2D2D2D");
            this.listViewPrincipal.FullRowSelect = true;
            this.listViewPrincipal.HideSelection = false;
            this.listViewPrincipal.Location = new System.Drawing.Point(0, 0);
            this.listViewPrincipal.Name = "listViewPrincipal";
            this.listViewPrincipal.OwnerDraw = true;
            this.listViewPrincipal.Size = new System.Drawing.Size(850, 610);
            this.listViewPrincipal.TabIndex = 0;
            this.listViewPrincipal.UseCompatibleStateImageBehavior = false;
            this.listViewPrincipal.View = System.Windows.Forms.View.Details;
            // 
            // colNombre
            // 
            this.colNombre.Text = "Nombre";
            this.colNombre.Width = 350;
            // 
            // colTipo
            // 
            this.colTipo.Text = "Tipo";
            this.colTipo.Width = 120;
            // 
            // colTamano
            // 
            this.colTamano.Text = "Tamaño";
            this.colTamano.Width = 120;
            // 
            // colInfo
            // 
            this.colInfo.Text = "Contenido / Info";
            this.colInfo.Width = 240;
            // 
            // treeViewLateral
            // 
            this.treeViewLateral.BackColor = System.Drawing.ColorTranslator.FromHtml("#FCE4EC");
            this.treeViewLateral.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.treeViewLateral.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeViewLateral.DrawMode = System.Windows.Forms.TreeViewDrawMode.OwnerDrawAll;
            this.treeViewLateral.ForeColor = System.Drawing.ColorTranslator.FromHtml("#2D2D2D");
            this.treeViewLateral.FullRowSelect = true;
            this.treeViewLateral.ItemHeight = 28;
            this.treeViewLateral.Location = new System.Drawing.Point(0, 50);
            this.treeViewLateral.Name = "treeViewLateral";
            this.treeViewLateral.ShowLines = false;
            this.treeViewLateral.ShowPlusMinus = false;
            this.treeViewLateral.Size = new System.Drawing.Size(348, 560);
            this.treeViewLateral.TabIndex = 1;
            // 
            // pnlSearch
            // 
            this.pnlSearch.BackColor = System.Drawing.ColorTranslator.FromHtml("#FCE4EC");
            this.pnlSearch.Controls.Add(this.btnBuscar);
            this.pnlSearch.Controls.Add(this.pnlSearchBorder);
            this.pnlSearch.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlSearch.Location = new System.Drawing.Point(0, 0);
            this.pnlSearch.Name = "pnlSearch";
            this.pnlSearch.Size = new System.Drawing.Size(348, 50);
            this.pnlSearch.TabIndex = 0;
            // 
            // btnBuscar
            // 
            this.btnBuscar.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBuscar.BackColor = System.Drawing.ColorTranslator.FromHtml("#F48FB1");
            this.btnBuscar.FlatAppearance.BorderSize = 0;
            this.btnBuscar.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnBuscar.ForeColor = System.Drawing.Color.White;
            this.btnBuscar.Location = new System.Drawing.Point(265, 10);
            this.btnBuscar.Name = "btnBuscar";
            this.btnBuscar.Size = new System.Drawing.Size(75, 30);
            this.btnBuscar.TabIndex = 1;
            this.btnBuscar.Text = "Buscar";
            this.btnBuscar.UseVisualStyleBackColor = false;
            // 
            // pnlSearchBorder
            // 
            this.pnlSearchBorder.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pnlSearchBorder.BackColor = System.Drawing.ColorTranslator.FromHtml("#F8BBD0");
            this.pnlSearchBorder.Controls.Add(this.txtBuscar);
            this.pnlSearchBorder.Location = new System.Drawing.Point(6, 10);
            this.pnlSearchBorder.Name = "pnlSearchBorder";
            this.pnlSearchBorder.Padding = new System.Windows.Forms.Padding(2);
            this.pnlSearchBorder.Size = new System.Drawing.Size(253, 30);
            this.pnlSearchBorder.TabIndex = 0;
            // 
            // txtBuscar
            // 
            this.txtBuscar.BackColor = System.Drawing.ColorTranslator.FromHtml("#FFF5F8");
            this.txtBuscar.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.txtBuscar.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtBuscar.Font = new System.Drawing.Font("Segoe UI", 11F);
            this.txtBuscar.ForeColor = System.Drawing.ColorTranslator.FromHtml("#2D2D2D");
            this.txtBuscar.Location = new System.Drawing.Point(2, 2);
            this.txtBuscar.Multiline = true;
            this.txtBuscar.Name = "txtBuscar";
            this.txtBuscar.Size = new System.Drawing.Size(249, 26);
            this.txtBuscar.TabIndex = 0;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.ColorTranslator.FromHtml("#FFF5F8");
            this.ClientSize = new System.Drawing.Size(1200, 700);
            this.Controls.Add(this.splitContainerMain);
            this.Controls.Add(this.pnlBottom);
            this.Controls.Add(this.pnlTop);
            this.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MinimumSize = new System.Drawing.Size(800, 500);
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "FileExplorerr";
            this.pnlTop.ResumeLayout(false);
            this.pnlAddressBorder.ResumeLayout(false);
            this.pnlAddressBorder.PerformLayout();
            this.pnlBottom.ResumeLayout(false);
            this.pnlBottom.PerformLayout();
            this.pnlTrash.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).EndInit();
            this.splitContainerMain.Panel1.ResumeLayout(false);
            this.splitContainerMain.Panel2.ResumeLayout(false);
            this.splitContainerMain.ResumeLayout(false);
            this.pnlSearch.ResumeLayout(false);
            this.pnlSearchBorder.ResumeLayout(false);
            this.pnlSearchBorder.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        // Declaración de Variables de los Controles
        public System.Windows.Forms.Panel pnlTop;
        public System.Windows.Forms.Button btnAtras;
        public System.Windows.Forms.Button btnSubir;
        public System.Windows.Forms.Panel pnlAddressBorder;
        public System.Windows.Forms.TextBox txtDireccion;
        public System.Windows.Forms.Button btnNuevaCarpeta;
        public System.Windows.Forms.Button btnActualizar;
        public System.Windows.Forms.Button btnExportarCSV;

        public System.Windows.Forms.Panel pnlBottom;
        public System.Windows.Forms.Label lblStatus;
        public System.Windows.Forms.Panel pnlTrash;
        public System.Windows.Forms.Label lblTrash;

        public System.Windows.Forms.SplitContainer splitContainerMain;
        public System.Windows.Forms.ListView listViewPrincipal;
        private System.Windows.Forms.ColumnHeader colNombre;
        private System.Windows.Forms.ColumnHeader colTipo;
        private System.Windows.Forms.ColumnHeader colTamano;
        private System.Windows.Forms.ColumnHeader colInfo;

        public System.Windows.Forms.Panel pnlSearch;
        public System.Windows.Forms.Panel pnlSearchBorder;
        public System.Windows.Forms.TextBox txtBuscar;
        public System.Windows.Forms.Button btnBuscar;
        public System.Windows.Forms.TreeView treeViewLateral;
    }
}