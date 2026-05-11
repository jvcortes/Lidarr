using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(082)]
    public class add_cover_art_provider_settings : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // Cover art provider credentials (Discogs PAT, Spotify Client ID/Secret,
            // Last.fm API Key) are stored in the existing Config key-value table.
            // No schema change is required; this migration is a version marker only.
        }
    }
}
