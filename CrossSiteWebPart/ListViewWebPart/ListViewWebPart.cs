using Microsoft.SharePoint;
using Microsoft.SharePoint.Utilities;
using Microsoft.SharePoint.WebControls;
using Microsoft.SharePoint.WebPartPages;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls.WebParts;

namespace CrossSiteWebPart.ListViewWebPart
{
    //[ToolboxItemAttribute(false)]
    [Guid("8c5236d9-e975-4e46-ad49-ab6fbd200f51")]
    public class ListViewWebPart : System.Web.UI.WebControls.WebParts.WebPart
    {
        #region protected child control variable definitions

        protected ListViewByQuery viewByQuery = null;
        protected EncodedLiteral encodedLiteral = null;

        #endregion protected child control variable definitions

        #region webpart properties

        /// <summary>
        /// View Name
        /// </summary>
        private string viewNameField = string.Empty;

        [Personalizable(), WebPartStorage(Storage.Shared), WebBrowsable(),
         WebDisplayName("View Name"), WebDescription("View Name")]
        public string ViewName
        {
            get
            {
                return this.viewNameField;
            }
            set
            {
                this.viewNameField = value;
            }
        }

        private string siteUrlField = string.Empty;

        [Personalizable(), WebPartStorage(Storage.Shared), WebBrowsable(),
         WebDisplayName("Site Url"), WebDescription("Site Url")]
        public string SiteUrl
        {
            get
            {
                return this.siteUrlField;
            }
            set
            {
                this.siteUrlField = value;
            }
        }

        /// <summary>
        ///
        /// </summary>
        private string sourceListField = string.Empty;

        [Personalizable(), WebPartStorage(Storage.Shared), WebBrowsable(),
         WebDisplayName("Source List"), WebDescription("Source list to query")]
        public string SourceList
        {
            get
            {
                return this.sourceListField;
            }
            set
            {
                this.sourceListField = value;
            }
        }

        private Boolean disableFilterField = false;

        [Personalizable(), WebPartStorage(Storage.Shared), WebBrowsable(),
         WebDisplayName("Disable Filter"), WebDescription("Disable List Filtering")]
        public Boolean DisableFilter
        {
            get
            {
                return disableFilterField;
            }
            set
            {
                disableFilterField = value;
            }
        }

        private Boolean disableSortField = false;

        [Personalizable(), WebPartStorage(Storage.Shared), WebBrowsable(),
         WebDisplayName("Disable Sort"), WebDescription("Disable list sorting")]
        public Boolean DisableSort
        {
            get
            {
                return disableSortField;
            }
            set
            {
                this.disableSortField = value;
            }
        }

        #endregion webpart properties

        #region overrides

        protected override void CreateChildControls()
        {
            

            //string pos = GetPageInfo();
            //GetPagin();

            // fix sorting and filter
            PropertyInfo isreadonly = typeof(System.Collections.Specialized.NameValueCollection).GetProperty("IsReadOnly", BindingFlags.Instance | BindingFlags.NonPublic);
            isreadonly.SetValue(this.Context.Request.QueryString, false, null);
            this.Context.Request.QueryString.Remove("View");
            isreadonly.SetValue(this.Context.Request.QueryString, true, null);

            GetPagin();

            base.CreateChildControls();

            SPSite site = null;
            SPWeb web = null;
            Boolean disposeSPSite = false;
            try
            {
                if (!string.IsNullOrEmpty(SiteUrl) && !string.IsNullOrWhiteSpace(SourceList))
                {
                    viewByQuery = new ListViewByQuery();

                    if (!string.IsNullOrEmpty(SiteUrl) && !string.IsNullOrWhiteSpace(SiteUrl))
                    {
                        // cross site
                        site = new SPSite(this.SiteUrl);
                        disposeSPSite = true;
                    }
                    else
                    {
                        // current context site
                        site = SPContext.Current.Site;
                    }

                    web = site.OpenWeb();

                    /*
                    Panel pnlDiv = new Panel();
                    pnlDiv.ID = "pnlDiv";
                    pnlDiv.CssClass = "ms-authoringcontrols";
                    */

                    SPSecurity.RunWithElevatedPrivileges(delegate()
                    {
                        SPSite CurrentSite = new SPSite(site.ID);
                        SPWeb CurrentWeb = CurrentSite.OpenWeb(web.ID);
                        SPList sourceList = web.Lists.TryGetList(SourceList);


                        /*
                         * get Context of EDF/MSH ( not work)
                        HttpRequest httpRequest = new HttpRequest("", CurrentWeb.Url, "");
                        HttpContext.Current = new HttpContext(httpRequest, new HttpResponse(new StringWriter()));
                        SPControl.SetContextWeb(HttpContext.Current, CurrentWeb);
                        */
                        // return if null or if list is missing
                        if (sourceList == null) return;

                        // Obtient ou définit une valeur booléenne qui spécifie s'il faut autoriser les mises à jour de la base de données
                        // à la suite d'une demande GET ou sans nécessiter qu'une validation de la sécurité.
                        // true si les mises à jour non sécurisées sont autorisées ; dans le cas contraire, false.
                        CurrentWeb.AllowUnsafeUpdates = false;

                        viewByQuery.List = sourceList;

                        SPQuery query = null;

                        if (CheckIfViewExists(viewByQuery.List))
                        {
                            //use the view specified in webpart property
                            query = new SPQuery(viewByQuery.List.Views[ViewName]);
                        }
                        else
                        {
                            //use default view to initialized
                            query = new SPQuery(viewByQuery.List.DefaultView);
                        }

                        // modifier dynamiquement le lien vers sites/edf/msh
                        // exemple : http://portal.gmsp15.dev/sites/ext/spie/SitePages/Home.aspx?Paged=TRUE&p_ID=6&View={5E2CD086-75DA-4F1A-ABE8-323A97D8A3B0}&FilterClear=1&PageFirstRow=6

                        // fonctionne pas
                        // pos = GetPageInfo();
                        //SPListItemCollectionPosition position = new SPListItemCollectionPosition(pos);
                        //query.ListItemCollectionPosition = position;
                        //SPUtility.Redirect(this.Context.Request.Url.GetLeftPart(UriPartial.Path), SPRedirectFlags.Default, this.Context, pos);

                        query.RowLimit = 10;

                        viewByQuery.Query = query;
                        viewByQuery.DisableFilter = DisableFilter;
                        viewByQuery.DisableSort = DisableSort;
                        Controls.Add(viewByQuery);
                    });
                }
                else
                {
                    encodedLiteral = new EncodedLiteral();
                    encodedLiteral.Text = "This webpart is not configured.";
                    Controls.Add(encodedLiteral);
                }
            }
            finally
            {
                if (disposeSPSite)
                {
                    ((IDisposable)site).Dispose();
                    ((IDisposable)web).Dispose();
                }
            }
        }

        protected override void RenderContents(HtmlTextWriter writer)
        {

            EnsureChildControls();
            RenderChildren(writer);
        }

        #endregion overrides

        #region helper methods

        // check if view exist
        private Boolean CheckIfViewExists(SPList list)
        {
            Boolean ret = false;

            foreach (SPView view in list.Views)
            {
                if (view.Title.ToLower() == this.ViewName.ToLower())
                {
                    ret = true;
                    break;
                }
            }
            return ret;
        }

        // wrong
        private string GetPageInfo()
        {
            string queryString = string.Empty;
            if (!string.IsNullOrEmpty(this.Context.Request.QueryString["Paged"]) && !string.IsNullOrEmpty(this.Context.Request.QueryString["View"]))
            {
                foreach (string key in this.Context.Request.QueryString.Keys)
                {
                    if (key.ToLower() != "view")
                    {
                        queryString += key + "=" + this.Context.Request.QueryString[key] + "&";
                    }
                }

                Console.WriteLine("{0}", queryString);

                // Return Left(queryString, Len(queryString) - 1)
                //return Strings.Left(queryString, Strings.Len(queryString) - 1);

                string left = queryString.Substring(0, queryString.Length - 1);

                // SPUtility.Redirect(this.Context.Request.Url.GetLeftPart(UriPartial.Path), SPRedirectFlags.Default, HttpContext.Current, queryString);

                return left;
                // return;
            }
            return queryString;
        }

        // wrong
        private void GetPagin()
        {
            if (!string.IsNullOrEmpty(this.Context.Request.QueryString["Paged"]) && !string.IsNullOrEmpty(this.Context.Request.QueryString["View"]))
            {
                string queryString = string.Empty;
                foreach (string key in this.Context.Request.QueryString.Keys)
                {
                    if (key.ToLower() != "view")
                    {
                        queryString += key + "=" + this.Context.Request.QueryString[key] + "&";
                    }
                }
                SPUtility.Redirect(this.Context.Request.Url.GetLeftPart(UriPartial.Path), SPRedirectFlags.Default, HttpContext.Current, queryString);
                //SPUtility.Redirect(this.Context.Request.Url.GetLeftPart(UriPartial.Path), SPRedirectFlags.Default, this.Context, queryString);
                return;
            }
        }

        private string BuildFilter()
        {
            string query = "{0}";
            bool isFound = true;
            int counter = 1;
            while (isFound)
            {
                string filterfield = "FilterField" + counter.ToString();
                string filtervalue = "FilterValue" + counter.ToString();

                if (this.Context.Request[filterfield] != null && this.Context.Request[filtervalue] != null)
                {
                    // Field type must be treated differently in case of other data type
                    if (counter > 1)
                    {
                        query = "<And>" + query + "{0}</And>";
                    }

                    query = string.Format(query, string.Format("<Eq><FieldRef Name='{0}' /><Value Type='Text'>{1}</Value></Eq>",
                            this.Context.Request[filterfield], this.Context.Request[filtervalue]));
                    counter++;
                }
                else
                {
                    isFound = false;
                }
            } // while

            // no good alway true
            /*
            if (!string.IsNullOrEmpty(query))
                query = "<Where>" + query + "</Where>";
            */
            if (!query.Equals("{0}"))
                query = string.Format("<Where>{0}</Where>", query);
            else query = string.Empty;

            return query;
        }

        #endregion helper methods
    }
}