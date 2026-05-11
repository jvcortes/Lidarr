import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import {
  clearCoverArtCandidates,
  fetchCoverArtCandidates,
  resetCoverArt,
  selectCoverArt
} from 'Store/Actions/coverArtActions';
import SelectCoverArtModalContent from './SelectCoverArtModalContent';

function createMapStateToProps() {
  return createSelector(
    (state) => state.coverArt,
    (coverArt) => {
      return {
        isFetching: coverArt.isFetching,
        isPopulated: coverArt.isPopulated,
        error: coverArt.error,
        isSaving: coverArt.isSaving,
        saveError: coverArt.saveError,
        items: coverArt.items
      };
    }
  );
}

const mapDispatchToProps = {
  fetchCoverArtCandidates,
  selectCoverArt,
  resetCoverArt,
  clearCoverArtCandidates
};

class SelectCoverArtModalConnector extends Component {

  //
  // Lifecycle

  componentDidMount() {
    this.props.fetchCoverArtCandidates({ albumId: this.props.albumId });
  }

  componentWillUnmount() {
    this.props.clearCoverArtCandidates();
  }

  //
  // Listeners

  onSelectPress = (coverUrl) => {
    this.props.selectCoverArt({
      albumId: this.props.albumId,
      coverUrl
    });
    this.props.onModalClose();
  };

  onResetPress = () => {
    this.props.resetCoverArt({ albumId: this.props.albumId });
    this.props.onModalClose();
  };

  //
  // Render

  render() {
    const {
      fetchCoverArtCandidates: _fetch,
      selectCoverArt: _select,
      resetCoverArt: _reset,
      clearCoverArtCandidates: _clear,
      ...otherProps
    } = this.props;

    return (
      <SelectCoverArtModalContent
        {...otherProps}
        onSelectPress={this.onSelectPress}
        onResetPress={this.onResetPress}
      />
    );
  }
}

SelectCoverArtModalConnector.propTypes = {
  albumId: PropTypes.number.isRequired,
  albumTitle: PropTypes.string.isRequired,
  isSaving: PropTypes.bool.isRequired,
  fetchCoverArtCandidates: PropTypes.func.isRequired,
  selectCoverArt: PropTypes.func.isRequired,
  resetCoverArt: PropTypes.func.isRequired,
  clearCoverArtCandidates: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(SelectCoverArtModalConnector);
