using System.ComponentModel;
using System.Globalization;
using DevExpress.Images;
using DevExpress.Utils;
using DevExpress.Utils.Svg;

namespace ERSoftSMSBox;

public partial class Form1
{
    private SvgImageCollection CreateMenuImages(IContainer container)
    {
        SvgImageCollection images = new(container);
        (string Key, string Resource)[] mappings =
        [
            ("HomePage", "svgimages/icon%20builder/actions_home.svg"),
            ("NewSms", "svgimages/icon%20builder/actions_send.svg"),
            ("BulkSms", "svgimages/outlook%20inspired/mailmerge.svg"),
            ("Templates", "svgimages/business%20objects/bo_document.svg"),
            ("Contacts", "svgimages/business%20objects/bo_contact.svg"),
            ("Groups", "svgimages/business%20objects/bo_department.svg"),
            ("ImportContacts", "svgimages/scheduling/import.svg"),
            ("SentMessages", "svgimages/outlook%20inspired/glyph_mail.svg"),
            ("DeliveryReports", "svgimages/icon%20builder/business_report.svg"),
            ("ConnectionSettings", "svgimages/dashboards/editconnection.svg"),
            ("IletiMerkezi", "svgimages/dashboards/editconnection.svg"),
            ("DatabaseSettings", "svgimages/dashboards/servermode.svg"),
            ("Offices", "svgimages/icon%20builder/shopping_store.svg"),
            ("Customers", "svgimages/outlook%20inspired/customers.svg"),
            ("PhoneBook", "svgimages/outlook%20inspired/customercontactdirectory.svg"),
            ("SentSms", "svgimages/outlook%20inspired/glyph_mail.svg"),
            ("NetGsm", "svgimages/icon%20builder/electronics_phoneandroid.svg"),
            ("ApplicationSettings", "svgimages/icon%20builder/actions_settings.svg"),
            ("Exit", "devav/actions/exit.svg"),
            ("SelectTheme", "svgimages/diagramicons/palette.svg"),
            ("Ready", "svgimages/icon%20builder/actions_checkcircled.svg")
        ];

        foreach ((string key, string resource) in mappings)
        {
            SvgImage image = ImageResourceCache.Default.GetSvgImage(resource)
                ?? throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                    GetText("MissingMenuIcon"), resource));
            images.Add(key, image.Clone());
        }

        return images;
    }
}
