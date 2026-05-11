using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(081)]
    public class add_user_selected_cover_url : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("Albums")
                 .AddColumn("UserSelectedCoverUrl").AsString().Nullable();
        }
    }
}
