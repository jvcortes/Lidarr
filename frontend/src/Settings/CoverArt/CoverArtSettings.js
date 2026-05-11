import React, { Component } from 'react';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import SettingsToolbarConnector from 'Settings/SettingsToolbarConnector';
import translate from 'Utilities/String/translate';
import CoverArtProviderSettingsConnector from './CoverArtProviderSettingsConnector';

class CoverArtSettings extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this._saveCallback = null;

    this.state = {
      isSaving: false,
      hasPendingChanges: false
    };
  }

  //
  // Listeners

  onChildMounted = (saveCallback) => {
    this._saveCallback = saveCallback;
  };

  onChildStateChange = (payload) => {
    this.setState(payload);
  };

  onSavePress = () => {
    if (this._saveCallback) {
      this._saveCallback();
    }
  };

  //
  // Render

  render() {
    const {
      isSaving,
      hasPendingChanges
    } = this.state;

    return (
      <PageContent title={translate('CoverArtProviderSettings')}>
        <SettingsToolbarConnector
          isSaving={isSaving}
          hasPendingChanges={hasPendingChanges}
          onSavePress={this.onSavePress}
        />

        <PageContentBody>
          <CoverArtProviderSettingsConnector
            onChildMounted={this.onChildMounted}
            onChildStateChange={this.onChildStateChange}
          />
        </PageContentBody>
      </PageContent>
    );
  }
}

export default CoverArtSettings;
