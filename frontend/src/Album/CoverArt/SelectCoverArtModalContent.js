import PropTypes from 'prop-types';
import React from 'react';
import Alert from 'Components/Alert';
import Icon from 'Components/Icon';
import Label from 'Components/Label';
import Button from 'Components/Link/Button';
import Link from 'Components/Link/Link';
import SpinnerButton from 'Components/Link/SpinnerButton';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { icons, kinds, sizes } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import styles from './SelectCoverArtModalContent.css';

const sourceKindMap = {
  iTunes: kinds.PRIMARY,
  MusicBrainz: kinds.INFO,
  Discogs: kinds.WARNING,
  Spotify: kinds.SUCCESS,
  LastFm: kinds.DEFAULT
};

function SelectCoverArtModalContent(props) {
  const {
    albumTitle,
    isFetching,
    isPopulated,
    error,
    isSaving,
    items,
    onSelectPress,
    onResetPress,
    onModalClose
  } = props;

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        {translate('SelectCoverArtForAlbum', { albumTitle })}
      </ModalHeader>

      <ModalBody>
        {
          isFetching && !isPopulated &&
            <LoadingIndicator />
        }

        {
          !isFetching && !!error &&
            <Alert kind={kinds.DANGER}>
              {translate('NoCoverArtCandidatesFound')}
            </Alert>
        }

        {
          isPopulated && !items.length &&
            <Alert kind={kinds.WARNING}>
              {translate('NoCoverArtCandidatesFound')}
            </Alert>
        }

        {
          isPopulated && !!items.length &&
            <div className={styles.grid}>
              {
                items.map((candidate, index) => {
                  const labelKind = sourceKindMap[candidate.source] ?? kinds.DEFAULT;
                  const resolution = (candidate.width && candidate.height) ?
                    `${candidate.width}\xD7${candidate.height}` :
                    null;

                  return (
                    <div key={index} className={styles.card}>
                      <div className={styles.sourceBadge}>
                        <Label kind={labelKind} size={sizes.SMALL}>
                          {candidate.source}
                        </Label>
                      </div>

                      <img
                        className={styles.thumbnail}
                        src={`${window.Lidarr.urlBase}${candidate.thumbnailUrl}`}
                        alt={candidate.releaseTitle ?? albumTitle}
                      />

                      <div className={styles.info}>
                        <span className={styles.resolution}>
                          {resolution ?? ''}
                        </span>

                        <SpinnerButton
                          className={styles.selectButton}
                          kind={kinds.PRIMARY}
                          size={sizes.SMALL}
                          isSpinning={isSaving}
                          onPress={() => onSelectPress(candidate.imageUrl)}
                        >
                          {translate('Select...')}
                        </SpinnerButton>
                      </div>

                      {
                        candidate.releaseUrl &&
                          <Link
                            className={styles.releaseLink}
                            to={candidate.releaseUrl}
                          >
                            <Icon name={icons.EXTERNAL_LINK} size={10} />
                            {' '}
                            {candidate.releaseTitle}
                          </Link>
                      }
                    </div>
                  );
                })
              }
            </div>
        }
      </ModalBody>

      <ModalFooter>
        <SpinnerButton
          kind={kinds.DANGER}
          isSpinning={isSaving}
          onPress={onResetPress}
        >
          {translate('ResetCoverArtToDefault')}
        </SpinnerButton>

        <Button onPress={onModalClose}>
          {translate('Close')}
        </Button>
      </ModalFooter>
    </ModalContent>
  );
}

SelectCoverArtModalContent.propTypes = {
  albumTitle: PropTypes.string.isRequired,
  isFetching: PropTypes.bool.isRequired,
  isPopulated: PropTypes.bool.isRequired,
  error: PropTypes.object,
  isSaving: PropTypes.bool.isRequired,
  items: PropTypes.arrayOf(PropTypes.object).isRequired,
  onSelectPress: PropTypes.func.isRequired,
  onResetPress: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired
};

SelectCoverArtModalContent.defaultProps = {
  items: []
};

export default SelectCoverArtModalContent;
