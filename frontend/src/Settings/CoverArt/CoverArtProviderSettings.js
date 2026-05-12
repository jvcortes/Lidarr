import PropTypes from 'prop-types';
import React from 'react';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import { inputTypes, kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';

function CoverArtProviderSettings(props) {
  const {
    isFetching,
    error,
    settings,
    hasSettings,
    onInputChange
  } = props;

  return (
    <div>
      {
        isFetching &&
          <LoadingIndicator />
      }

      {
        !isFetching && error &&
          <Alert kind={kinds.DANGER}>
            {translate('UnableToLoadMetadataProviderSettings')}
          </Alert>
      }

      {
        hasSettings && !isFetching && !error &&
          <Form>
            <FieldSet legend={translate('CoverArtProviders')}>

              <FormGroup>
                <FormLabel>
                  {translate('EnableMusicBrainz')}
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.CHECK}
                  name="enableMusicBrainz"
                  onChange={onInputChange}
                  {...settings.enableMusicBrainz}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>
                  {translate('EnableItunes')}
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.CHECK}
                  name="enableItunes"
                  onChange={onInputChange}
                  {...settings.enableItunes}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>
                  {translate('EnableDiscogs')}
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.CHECK}
                  name="enableDiscogs"
                  onChange={onInputChange}
                  {...settings.enableDiscogs}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>
                  {translate('EnableSpotify')}
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.CHECK}
                  name="enableSpotify"
                  onChange={onInputChange}
                  {...settings.enableSpotify}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>
                  {translate('EnableLastFm')}
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.CHECK}
                  name="enableLastFm"
                  onChange={onInputChange}
                  {...settings.enableLastFm}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>
                  {translate('CoverArtDiscogsToken')}
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.PASSWORD}
                  name="discogsToken"
                  helpText={translate('CoverArtDiscogsTokenHelpText')}
                  onChange={onInputChange}
                  {...settings.discogsToken}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>
                  {translate('CoverArtSpotifyClientId')}
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.TEXT}
                  name="spotifyClientId"
                  helpText={translate('CoverArtSpotifyClientIdHelpText')}
                  onChange={onInputChange}
                  {...settings.spotifyClientId}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>
                  {translate('CoverArtSpotifyClientSecret')}
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.PASSWORD}
                  name="spotifyClientSecret"
                  helpText={translate('CoverArtSpotifyClientSecretHelpText')}
                  onChange={onInputChange}
                  {...settings.spotifyClientSecret}
                />
              </FormGroup>

              <FormGroup>
                <FormLabel>
                  {translate('CoverArtLastFmApiKey')}
                </FormLabel>

                <FormInputGroup
                  type={inputTypes.TEXT}
                  name="lastFmApiKey"
                  helpText={translate('CoverArtLastFmApiKeyHelpText')}
                  onChange={onInputChange}
                  {...settings.lastFmApiKey}
                />
              </FormGroup>

            </FieldSet>
          </Form>
      }
    </div>
  );
}

CoverArtProviderSettings.propTypes = {
  advancedSettings: PropTypes.bool.isRequired,
  isFetching: PropTypes.bool.isRequired,
  error: PropTypes.object,
  settings: PropTypes.object.isRequired,
  hasSettings: PropTypes.bool.isRequired,
  onInputChange: PropTypes.func.isRequired
};

export default CoverArtProviderSettings;
