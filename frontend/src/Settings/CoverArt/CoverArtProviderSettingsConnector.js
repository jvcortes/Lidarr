import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { clearPendingChanges } from 'Store/Actions/baseActions';
import {
  fetchCoverArtProviderSettings,
  saveCoverArtProviderSettings,
  setCoverArtProviderSettingsValue
} from 'Store/Actions/settingsActions';
import createSettingsSectionSelector from 'Store/Selectors/createSettingsSectionSelector';
import CoverArtProviderSettings from './CoverArtProviderSettings';

const SECTION = 'coverArtProviders';

function createMapStateToProps() {
  return createSelector(
    (state) => state.settings.advancedSettings,
    createSettingsSectionSelector(SECTION),
    (advancedSettings, sectionSettings) => {
      return {
        advancedSettings,
        ...sectionSettings
      };
    }
  );
}

const mapDispatchToProps = {
  dispatchFetchCoverArtProviderSettings: fetchCoverArtProviderSettings,
  dispatchSetCoverArtProviderSettingsValue: setCoverArtProviderSettingsValue,
  dispatchSaveCoverArtProviderSettings: saveCoverArtProviderSettings,
  dispatchClearPendingChanges: clearPendingChanges
};

class CoverArtProviderSettingsConnector extends Component {

  //
  // Lifecycle

  componentDidMount() {
    const {
      dispatchFetchCoverArtProviderSettings,
      dispatchSaveCoverArtProviderSettings,
      onChildMounted
    } = this.props;

    dispatchFetchCoverArtProviderSettings();
    onChildMounted(dispatchSaveCoverArtProviderSettings);
  }

  componentDidUpdate(prevProps) {
    const {
      hasPendingChanges,
      isSaving,
      onChildStateChange
    } = this.props;

    if (
      prevProps.isSaving !== isSaving ||
      prevProps.hasPendingChanges !== hasPendingChanges
    ) {
      onChildStateChange({
        isSaving,
        hasPendingChanges
      });
    }
  }

  componentWillUnmount() {
    this.props.dispatchClearPendingChanges({ section: 'settings.coverArtProviders' });
  }

  //
  // Listeners

  onInputChange = ({ name, value }) => {
    this.props.dispatchSetCoverArtProviderSettingsValue({ name, value });
  };

  //
  // Render

  render() {
    return (
      <CoverArtProviderSettings
        onInputChange={this.onInputChange}
        {...this.props}
      />
    );
  }
}

CoverArtProviderSettingsConnector.propTypes = {
  isSaving: PropTypes.bool.isRequired,
  hasPendingChanges: PropTypes.bool.isRequired,
  dispatchFetchCoverArtProviderSettings: PropTypes.func.isRequired,
  dispatchSetCoverArtProviderSettingsValue: PropTypes.func.isRequired,
  dispatchSaveCoverArtProviderSettings: PropTypes.func.isRequired,
  dispatchClearPendingChanges: PropTypes.func.isRequired,
  onChildMounted: PropTypes.func.isRequired,
  onChildStateChange: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(CoverArtProviderSettingsConnector);
